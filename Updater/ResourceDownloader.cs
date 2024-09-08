using Downloader;

using ForgeUpdater.Manifests;

using Octodiff.Core;
using Octodiff.Diagnostics;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;


namespace ForgeUpdater.Updater {
    internal class ResourceDownloader<TManifest> where TManifest : Manifest {
        private TManifest? SourceManifest { get; }
        private TManifest TargetManifest { get; }

        public ResourceDownloader(TManifest? from, TManifest to) {
            if (string.IsNullOrEmpty(to.Url))
                throw new ArgumentException("Manifest does not have a download URL.");

            if (from != null && to.Id != from.Id)
                throw new ArgumentException("Manifests are not for the same resource. Ids differ!");

            SourceManifest = from;
            TargetManifest = to;
        }

        public event EventHandler<DownloadProgressChangedEventArgs>? DownloadProgressChanged;
        public event EventHandler<AsyncCompletedEventArgs>? DownloadFileCompleted;

        string Name => TargetManifest.Id;

        string? SourceVersion => SourceManifest?.Version.ToString();
        string TargetVersion => TargetManifest.Version.ToString();

        /// <summary>
        /// The URI for the remote resource.
        /// </summary>
        string URI => $"{TargetManifest.Url!.TrimEnd('/')}/{Name}.{TargetVersion}.zip";
        string ChecksumURI => URI + ".sha1";
        string? DeltaPatchURI => SourceVersion != null ? URI + $".{SourceVersion}.delta" : null;

        string? SourceZipPath => SourceVersion != null ? $"{UpdaterConfig.BaseDownloadPath}/{Name}.{SourceVersion}.zip" : null;
        string TargetZipPath => $"{UpdaterConfig.BaseDownloadPath}/{Name}.{TargetVersion}.zip";
        string TargetDeltaPatchPath => TargetZipPath + $".{SourceVersion}.delta";

        bool HasBaseFile => File.Exists(SourceZipPath);
        bool CanDeltaPatch => HasBaseFile;

        int RetryCount { get; set; } = 3;

        /// <summary>
        /// Whether a delta patch is on the remote server available.
        /// </summary>
        [MemberNotNullWhen(true, nameof(DeltaPatchURI))]
        bool IsDeltaPatchAvailable() {
            if (DeltaPatchURI == null) return false;

            // Make a HEAD request to the server to check if the delta patch is available.
            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(DeltaPatchURI))).Result;
            return response.IsSuccessStatusCode;
        }

        public async Task<string> Download() {
            DownloadConfiguration configuration = new DownloadConfiguration {
                Timeout = (int)TimeSpan.FromSeconds(30).TotalMilliseconds,
                ReserveStorageSpaceBeforeStartingDownload = true,
            };

            string finalDownloadUri = URI;
            string finalZipPath = TargetZipPath;
            bool deltaPatch = false;
            if (CanDeltaPatch && IsDeltaPatchAvailable()) {
                finalDownloadUri = DeltaPatchURI;
                finalZipPath = TargetDeltaPatchPath;
                deltaPatch = true;
            }

            IDownload download = DownloadBuilder.New()
                .WithConfiguration(configuration)
                .WithUrl(finalDownloadUri)
                .WithFileLocation(finalZipPath)
                .Build();

            TaskCompletionSource<bool> downloadCompleted = new TaskCompletionSource<bool>();
            download.DownloadFileCompleted += (sender, args) => {

                if (args.Cancelled) {
                    UpdaterLogger.LogWarn("Download of %s was cancelled.", Name);
                    downloadCompleted.SetResult(false);
                    return;
                }

                if (args.Error != null) {
                    UpdaterLogger.LogError(args.Error, "Failed to download %s", Name);
                    downloadCompleted.SetResult(false);
                    return;
                }

                UpdaterLogger.LogInfo("Download of %s completed successfully.", Name);
                downloadCompleted.SetResult(true);
            };

            download.DownloadProgressChanged += DownloadProgressChanged;
            download.DownloadFileCompleted += DownloadFileCompleted;

            await download.StartAsync();

            bool result = await downloadCompleted.Task;
            if (!result) {
                throw new Exception("Failed to download resource.");
            }

            if (deltaPatch && !ApplyDeltaPatch()) {
                UpdaterLogger.LogWarn("Failed to apply delta patch to %s", Name);

                Cleanup();
                Retry();
            }


            if (!CheckDownload()) {
                UpdaterLogger.LogWarn("Checksum verification failed for %s", Name);

                Cleanup();
                Retry();
            }

            return TargetZipPath;
        }

        private async void Retry() {
            if (RetryCount <= 0)
                throw new Exception("Failed to download resource.");

            RetryCount--;

            UpdaterLogger.LogInfo("Retrying download of %s, %d attempts left.", Name, RetryCount);
            await Download();
        }

        /// <summary>
        /// Apply patch to downloaded file:
        /// </summary>
        /// <exception cref="FileNotFoundException"></exception>
        private bool ApplyDeltaPatch() {
            if (!File.Exists(SourceZipPath))
                throw new FileNotFoundException("Source zip file not found.");
            if (!File.Exists(TargetDeltaPatchPath))
                throw new FileNotFoundException("Delta patch file not found.");

            UpdaterLogger.LogInfo("Applying delta patch to %s", SourceZipPath);

            string intermediatePatchZipName = SourceZipPath + ".old";
            if (File.Exists(intermediatePatchZipName))
                File.Delete(intermediatePatchZipName);

            try {
                File.Move(SourceZipPath, intermediatePatchZipName, true);

                string finalZipPath = SourceZipPath;

                var deltaApplier = new DeltaApplier { SkipHashCheck = false };
                using (var basisStream = new FileStream(intermediatePatchZipName, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var deltaStream = new FileStream(TargetDeltaPatchPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var newFileStream = new FileStream(finalZipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read)) {
                    deltaApplier.Apply(basisStream, new BinaryDeltaReader(deltaStream, new ConsoleProgressReporter()), newFileStream);
                }

                UpdaterLogger.LogDebug("Delta patch applied successfully to %s", SourceZipPath);

                File.Delete(TargetDeltaPatchPath);
                File.Delete(intermediatePatchZipName);

                return true;
            } catch (Exception e) {
                UpdaterLogger.LogError(e, "Failed to apply delta patch to %s", SourceZipPath);

                if (File.Exists(SourceZipPath))
                    File.Delete(SourceZipPath);
                if (File.Exists(intermediatePatchZipName))
                    File.Delete(intermediatePatchZipName);

                return false;
            }
        }

        private bool CheckDownload() {
            if (!File.Exists(TargetZipPath)) {
                UpdaterLogger.LogError(null, "Download of %s failed.", Name);
                return false;
            }

            HttpClient client = new HttpClient();
            try {
                string remoteChecksum = client.GetStringAsync(ChecksumURI).Result;

                try {
                    SHA1 sha1 = SHA1.Create();
                    using FileStream fileStream = File.OpenRead(TargetZipPath);
                    byte[] localChecksum = sha1.ComputeHash(fileStream);
                    return string.Equals(BitConverter.ToString(localChecksum).Replace("-", string.Empty), remoteChecksum, StringComparison.InvariantCultureIgnoreCase);
                } catch (Exception e) {
                    UpdaterLogger.LogError(e, "Failed to verify checksum for %s", Name);
                    return false;
                }
            } catch (Exception) {
                UpdaterLogger.LogWarn("Failed to download checksum for %s", Name);
                return true;
            }
        }

        private void Cleanup() {
            UpdaterLogger.LogDebug("Cleaning up failed download of %s", Name);

            if (File.Exists(TargetZipPath))
                File.Delete(TargetZipPath);
            if (File.Exists(TargetDeltaPatchPath))
                File.Delete(TargetDeltaPatchPath);
        }
    }
}
