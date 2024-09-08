using ForgeUpdater.Manifests;

using System.IO.Compression;


namespace ForgeUpdater.Updater {
    internal class ResourceUpdater<TManifest> where TManifest : Manifest {
        private static readonly Dictionary<string, Action<string>> InstallerActions = new Dictionary<string, Action<string>>() {
            { "move", MoveAction },
            { "copy", (line) => { } },
            { "delete", (line) => { } },
            { "mkdir", (line) => { } }
        };

        public ResourceUpdater(TManifest target, string updateZip, string targetFolder) {
            TargetManifest = target;
            UpdateZip = updateZip;

            TargetFolder = targetFolder;
        }

        bool ShouldClearResidualFiles => TargetManifest.ClearResidualFiles;

        TManifest TargetManifest { get; init; }
        string UpdateZip { get; init; }
        private string TargetFolder { get; init; }

        public IEnumerable<float> Update() {
            ZipArchive zip = ZipFile.OpenRead(UpdateZip);
            HandleActionScript(zip);

            if (ShouldClearResidualFiles) {
                ClearResidualFiles();
            }

            int fileCount = zip.Entries.Count;
            int currentFile = 0;
            foreach (ZipArchiveEntry entry in zip.Entries) {
                // Report progress...
                yield return (float)currentFile++ / fileCount;

                string targetPath = Path.Combine(TargetFolder, entry.FullName);

                if (entry.FullName.EndsWith("/")) {
                    UpdaterLogger.LogDebug("Creating directory: {0}", targetPath);
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                if (File.Exists(targetPath) && IsFileIgnored(entry.FullName)) {
                    UpdaterLogger.LogDebug("Ignoring existing file: {0}", targetPath);
                } else {
                    UpdaterLogger.LogDebug("Extracting file: {0}", targetPath);
                    entry.ExtractToFile(targetPath, true);
                }
            }
        }

        private void ClearResidualFiles() {
            foreach (string file in Directory.GetFiles(TargetFolder, "*", SearchOption.AllDirectories)) {
                string relativePath = file.Substring(TargetFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                if (IsFileIgnored(relativePath)) {
                    UpdaterLogger.LogDebug("Ignoring residual file: {0}", file);
                    continue;
                }

                UpdaterLogger.LogDebug("Deleting residual file: {0}", file);
                File.Delete(file);
            }
        }

        private void HandleActionScript(ZipArchive zip) {
            ZipArchiveEntry? actionScript = zip.Entries.FirstOrDefault((e) => e.Name == "forge_script.txt");
            if (actionScript == null) return;

            using StreamReader reader = new StreamReader(actionScript.Open());
            string[] actionLines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            ApplyInstallerActions(actionLines);
        }

        private void ApplyInstallerActions(string[] actionLines) {
            foreach (string actionLine in actionLines) {
                try {
                    string actionName = actionLine.Split(' ')[0];

                    if (InstallerActions.TryGetValue(actionName, out Action<string>? actionHandler)) {
                        actionHandler(actionLine);
                    } else {
                        UpdaterLogger.LogWarn("Unknown installer action: {0}", actionName);
                    }
                } catch (Exception e) {
                    UpdaterLogger.LogError(e, "Failed to parse installer action: {0}", actionLine);
                }
            }
        }

        private bool IsFileIgnored(string file) {
            if (TargetManifest.IgnoredEntries == null)
                return false;

            foreach (string ignore in TargetManifest.IgnoredEntries) {
                bool ignoreIsDirectory = ignore.EndsWith("/");

                if (ignoreIsDirectory) {
                    if (file.StartsWith(ignore, StringComparison.InvariantCultureIgnoreCase))
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
                File.Delete(target);
            }

            UpdaterLogger.LogDebug("Moving {0} to {1}", source, target);
            File.Move(source, target);
        }

        #endregion
    }
}
