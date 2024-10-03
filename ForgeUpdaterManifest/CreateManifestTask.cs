using ForgeUpdater;
using ForgeUpdater.Manifests;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace ForgeUpdaterManifest {
    public class CreateManifestTask : Microsoft.Build.Utilities.Task {
        [Required]
        public string ManifestId { get; set; } = null!;
        [Required]
        public string ManifestName { get; set; } = null!;
        [Required]
        public string ManifestVersion { get; set; } = null!;
        [Required]
        public string ManifestType { get; set; } = null!;

        [Required]
        public string ManifestAssetUrl { get; set; } = null!;
        public bool ManifestClearResidualFiles { get; set; } = false;
        public bool ManifestEmbedded { get; set; } = false;

        [Required]
        public string ManifestOutputFolder { get; set; } = null!;
        [Output]
        public string ManifestOutputPath { get; set; } = null!;

        public ITaskItem[]? ManifestIgnoredEntries { get; set; } = null;
        public ITaskItem[]? ManifestRelationships { get; set; } = null;

        class MsBuildUpdateLogger(TaskLoggingHelper Log) : IUpdaterLogger {
            public void LogInfo(string message, params object[] args) {
                Log.LogMessage(MessageImportance.Normal, message, args);
            }

            public void LogWarn(string message, params object[] args) {
                Log.LogWarning(message, args);
            }

            public void LogDebug(string message, params object[] args) {
                Log.LogMessage(MessageImportance.Low, message, args);
            }

            public void LogError(Exception? err, string message, params object[] args) {
                Log.LogError(message, args);
                if (err != null) {
                    Log.LogErrorFromException(err, true);
                }
            }
        }

        public override bool Execute() {
            UpdaterLogger.Logger = new MsBuildUpdateLogger(Log);

            Manifest m = new Manifest() {
                Id = ManifestId,
                Name = ManifestName,
                Version = ManifestVersion,
                Type = ManifestType,
                ClearResidualFiles = ManifestClearResidualFiles,
                Assets = new ManifestDownload {
                    AssetURI = ManifestAssetUrl,
                },
                Embedded = ManifestEmbedded
            };


            if (ManifestIgnoredEntries is { Length: > 0 })
                m.IgnoredEntries = ManifestIgnoredEntries.Select(i => i.ItemSpec).ToArray();

            if (ManifestRelationships is { Length: > 0 })
                m.Relationships = ManifestRelationships.Select(i => new Relationship() {
                    Id = i.ItemSpec,
                    ManifestUrl = i.GetMetadata("Manifest"),
                    Optional = bool.Parse(i.GetMetadata("Optional") ?? "false"),
                    Compatibility = new Compatibility {
                        Minimum = i.GetMetadata("Minimum") ??
                              throw new ArgumentException(
                                  $"Missing minimum compatible version for dependency {ManifestName}", "Minimum"),
                        Maximum = i.GetMetadata("Maximum"),
                        Verified = i.GetMetadata("Verified")
                    }
                }).ToArray() ?? Array.Empty<Relationship>();

            try {
                ManifestOutputPath = ManifestOutputFolder.TrimEnd('/', '\\') + "\\manifest.json";
                m.Save(ManifestOutputPath);

                return true;
            } catch (Exception e) {
                Log.LogErrorFromException(e, true);
                return false;
            }
        }
    }
}
