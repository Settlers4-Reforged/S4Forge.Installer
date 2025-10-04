﻿using ForgeUpdater.Manifests;

using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

using System;
using System.Linq;
using System.Xml.Schema;


namespace ForgeUpdater.Updater {
    internal class ResourceUpdater<TManifest> where TManifest : Manifest {
        private static readonly Dictionary<string, Action<string>> InstallerActions = new Dictionary<string, Action<string>>() {
            { "move", MoveAction },
            { "copy", CopyAction },
            { "delete", DeleteAction },
            { "mkdir", MkdirAction }
        };

        public ResourceUpdater(TManifest target, string updateZip, string targetFolder) {
            TargetManifest = target;
            UpdateZip = updateZip;

            TargetFolder = targetFolder;
        }

        bool ShouldClearResidualFiles => TargetManifest.ClearResidualFiles;

        TManifest TargetManifest { get; set; }
        string UpdateZip { get; set; }
        private string TargetFolder { get; set; }

        private static string LockFilePath(TManifest manifest) => Path.Combine(UpdaterConfig.BaseDownloadPath, $"{manifest.Id}-{manifest.Version}.lock");
        private string LockFile => LockFilePath(TargetManifest);

        public IEnumerable<float> Update() {
            if (File.Exists(LockFile)) {
                UpdaterLogger.LogWarn("[{0}] Found existing lock file {1}! Installation is maybe broken for {2}, retrying to apply update", TargetManifest.Name, LockFile, TargetManifest.Id);
            }

            // Ensure base path exists
            Directory.CreateDirectory(UpdaterConfig.BaseDownloadPath);
            File.Open(LockFile, FileMode.Create).Dispose();

            UpdaterLogger.LogDebug("[{0}] Opening zip file: {1}", TargetManifest.Name, UpdateZip);
            using ZipFile zip = new ZipFile(UpdateZip);

            Directory.CreateDirectory(TargetFolder);

            HandleActionScript(zip);

            if (Directory.Exists(TargetFolder)) {
                if (ShouldClearResidualFiles) {
                    ClearResidualFiles();
                }
            }

            long fileCount = zip.Count;
            long currentFile = 0;

            long errors = 0;

            yield return 0;

            UpdaterLogger.LogInfo("[{0}] Extracting {1} files to {2}...", TargetManifest.Name, fileCount, TargetFolder);

            Parallel.ForEach(zip.OfType<ZipEntry>(), (ZipEntry entry) => {
                // Report progress...
                Interlocked.Increment(ref currentFile);

                string targetPath = Path.Combine(TargetFolder, entry.Name);
                string targetDir = Path.GetDirectoryName(targetPath) ?? string.Empty;

                if (entry.Name.EndsWith("/")) {
                    UpdaterLogger.LogDebug("[{0}] Creating directory: {1}", TargetManifest.Name, targetPath);
                    Directory.CreateDirectory(targetPath);
                    return;
                }

                if (!Directory.Exists(targetDir)) {
                    UpdaterLogger.LogDebug("[{0}] Creating missing directory: {1}", TargetManifest.Name, targetPath);
                    Directory.CreateDirectory(targetDir);
                }

                if (File.Exists(targetPath) && IsFileIgnored(entry.Name)) {
                    UpdaterLogger.LogDebug("[{0}] Ignoring existing file: {1}", TargetManifest.Name, targetPath);
                } else {
                    UpdaterLogger.LogDebug("[{0}] Extracting file: {1}", TargetManifest.Name, targetPath);

                    try {
                        SafeDeleteFile(targetPath);

                        byte[] buffer = new byte[4096];
                        Stream zipStream = zip.GetInputStream(entry);
                        using FileStream streamWriter = File.Create(targetPath);
                        StreamUtils.Copy(zipStream, streamWriter, new byte[4096]);
                    } catch (Exception e) {
                        UpdaterLogger.LogError(e, "[{0}] Failed to extract file: {1}", TargetManifest.Name, targetPath);
                        Interlocked.Increment(ref errors);
                        return;
                    }
                }
            });

            if (errors > 0) {
                UpdaterLogger.LogError(null, "[{0}] Failed to extract {1} files. Please fix the errors and try again", TargetManifest.Name, errors);
            } else {
                UpdaterLogger.LogInfo("[{0}] Extracted {1} files.", TargetManifest.Name, fileCount);

                File.Delete(LockFile);
            }

            yield return 1;
        }

        private void ClearResidualFiles() {
            foreach (string file in Directory.GetFiles(TargetFolder, "*", SearchOption.AllDirectories)) {
                string relativePath = file.Substring(TargetFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                if (IsFileIgnored(relativePath)) {
                    UpdaterLogger.LogDebug("[{0}] Ignoring residual file: {1}", TargetManifest.Name, file);
                    continue;
                }

                UpdaterLogger.LogDebug("[{0}] Deleting residual file: {1}", TargetManifest.Name, file);
                SafeDeleteFile(file);
            }
        }

        private static void SafeDeleteFile(string file) {
            if (!File.Exists(file)) return;

            try {
                File.Delete(file);
            } catch (Exception e) {
                UpdaterLogger.LogWarn("Failed to delete file: {0}, trying to rename it and delete later. Error {1}", file, e);

                try {
                    // Open C# assemblies are locked by handle only, but the file can be still be renamed.
                    File.Move(file, file + ".updater_leftover");
                } catch (Exception e2) {
                    UpdaterLogger.LogError(e2, "Failed to delete and rename file: {0}", file);
                    throw;
                }
            }
        }

        public static bool IsInstallBroken(TManifest manifest) {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            return File.Exists(LockFilePath(manifest));
        }

        public static void CleanupLeftoverFiles(string folder) {
            foreach (string file in Directory.GetFiles(folder, "*.updater_leftover", SearchOption.AllDirectories)) {
                UpdaterLogger.LogDebug("Deleting leftover file: {0}", file);
                File.Delete(file);
            }
        }

        private void HandleActionScript(ZipFile zip) {
            UpdaterLogger.LogInfo("[{0}] Checking for installer action script...", TargetManifest.Name);

            ZipEntry? actionScript = zip.OfType<ZipEntry>().FirstOrDefault((e) => e.Name == "update_script.txt");
            if (actionScript == null) {
                UpdaterLogger.LogInfo("[{0}] No installer action script found.", TargetManifest.Name);
                return;
            }

            UpdaterLogger.LogInfo("[{0}] Found installer action script: {1}", TargetManifest.Name, actionScript.Name);
            using StreamReader reader = new StreamReader(zip.GetInputStream(actionScript));
            string[] actionLines = reader.ReadToEnd().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            UpdaterLogger.LogDebug("[{0}] Read {1} installer actions.", TargetManifest.Name, actionLines.Length);
            ApplyInstallerActions(actionLines);
        }

        private void ApplyInstallerActions(string[] actionLines) {
            UpdaterLogger.LogInfo("[{0}] Applying {1} installer actions...", TargetManifest.Name, actionLines.Length);
            foreach (string actionLine in actionLines) {
                UpdaterLogger.LogDebug("[{0}] Installer action: {1}", TargetManifest.Name, actionLine);

                try {
                    string actionName = actionLine.Split(' ')[0];

                    if (InstallerActions.TryGetValue(actionName, out Action<string>? actionHandler)) {
                        actionHandler(actionLine);
                    } else {
                        UpdaterLogger.LogWarn("[{0}] Unknown installer action: {1}", TargetManifest.Name, actionName);
                    }
                } catch (Exception e) {
                    UpdaterLogger.LogError(e, "[{0}] Failed to parse installer action: {1}", TargetManifest.Name, actionLine);
                }
            }

            UpdaterLogger.LogInfo("[{0}] Finished applying installer actions.", TargetManifest.Name);
        }

        private bool IsFileIgnored(string file) {
            if (file.Equals("manifest.json", StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (TargetManifest.IgnoredEntries == null)
                return false;

            foreach (string ignore in TargetManifest.IgnoredEntries) {
                string sanitizedIgnore = ignore.Replace('\\', '/');
                bool ignoreIsDirectory = sanitizedIgnore.EndsWith("/");

                if (ignoreIsDirectory) {
                    string sanitizedFile = file.Replace("\\", "/");
                    if (sanitizedFile.StartsWith(sanitizedIgnore, StringComparison.InvariantCultureIgnoreCase))
                        return true;
                } else {
                    if (string.Equals(file, ignore, StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        #region Actions

        static void MoveAction(string line) {
            string[] parts = line.Split(' ');

            if (parts.Length != 3) {
                UpdaterLogger.LogError(null, "Invalid move action: {0}", line);
                return;
            }

            string source = parts[1];
            string target = parts[2];

            if (File.Exists(target)) {
                UpdaterLogger.LogDebug("Deleting existing file: {0}", target);
                SafeDeleteFile(target);
            }

            UpdaterLogger.LogDebug("Moving {0} to {1}", source, target);
            File.Move(source, target);
        }

        static void CopyAction(string line) {
            string[] parts = line.Split(' ');

            if (parts.Length != 3) {
                UpdaterLogger.LogError(null, "Invalid copy action: {0}", line);
                return;
            }

            string source = parts[1];
            string target = parts[2];

            if (File.Exists(target)) {
                UpdaterLogger.LogDebug("Deleting existing file: {0}", target);
                SafeDeleteFile(target);
            }

            UpdaterLogger.LogDebug("Copying {0} to {1}", source, target);
            File.Copy(source, target);
        }

        static void DeleteAction(string line) {
            string[] parts = line.Split(' ');

            if (parts.Length != 2) {
                UpdaterLogger.LogError(null, "Invalid delete action: {0}", line);
                return;
            }

            string target = parts[1];

            if (File.Exists(target)) {
                UpdaterLogger.LogDebug("Deleting existing file: {0}", target);
                SafeDeleteFile(target);
            }
        }

        static void MkdirAction(string line) {
            string[] parts = line.Split(' ');

            if (parts.Length != 2) {
                UpdaterLogger.LogError(null, "Invalid mkdir action: {0}", line);
                return;
            }

            string target = parts[1];

            if (Directory.Exists(target)) {
                UpdaterLogger.LogDebug("Deleting existing directory: {0}", target);
                Directory.Delete(target, true);
            }

            UpdaterLogger.LogDebug("Creating directory: {0}", target);
            Directory.CreateDirectory(target);
        }

        #endregion
    }
}
