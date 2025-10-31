using Downloader;

using ForgeUpdater.Manifests;

using Octodiff.Core;
using Octodiff.Diagnostics;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;

using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;


namespace ForgeUpdater.Updater {
    internal class ResourceDownloader<TManifest> where TManifest : Manifest {
        private TManifest? SourceManifest { get; }
        private TManifest TargetManifest { get; }

        public ResourceDownloader(TManifest? from, TManifest to) {
            if (string.IsNullOrEmpty(to.Assets?.AssetURI))
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
        string URI => TargetManifest.Assets?.AssetURI!;
        string ChecksumURI => URI + ".sha1";

        string? DeltaPatchURI => TargetManifest.Assets?.DeltaPatchUrlFromVersion(SourceVersion)?.DeltaURI;
        string? DeltaSourceZipPath => TargetManifest.Assets?.DeltaPatchUrlFromVersion(SourceVersion)?.SourceFileName;

        string TargetZipPath => $"{UpdaterConfig.BaseDownloadPath}/{Name}.{TargetVersion}.zip";
        string TargetDeltaPatchPath => TargetZipPath + $".{SourceVersion}.delta";

        bool HasBaseFile => File.Exists(DeltaSourceZipPath);
        bool CanDeltaPatch => HasBaseFile;

        int RetryCount { get; set; } = 3;

        /// <summary>
        /// Whether a delta patch is on the remote server available.
        /// </summary>
        bool IsDeltaPatchAvailable() {
            if (DeltaPatchURI == null) return false;

            // Make a HEAD request to the server to check if the delta patch is available.
            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(DeltaPatchURI))).Result;
            if (!response.IsSuccessStatusCode)
                UpdaterLogger.LogWarn("Delta patch was set for {0} but not found at {1} (HTTP {2})", Name, DeltaPatchURI, (int)response.StatusCode);

            return response.IsSuccessStatusCode;
        }

        public async Task<string> Download() {
            DownloadConfiguration configuration = new DownloadConfiguration {
                Timeout = (int)TimeSpan.FromSeconds(30).TotalMilliseconds,
                ReserveStorageSpaceBeforeStartingDownload = true
            };

            string finalDownloadUri = URI ?? throw new ArgumentNullException(nameof(URI), "Download URI is null");
            string finalZipPath = TargetZipPath;
            bool deltaPatch = false;
            if (CanDeltaPatch && IsDeltaPatchAvailable()) {
                UpdaterLogger.LogInfo("Delta patch available for {0}, downloading delta patch from {1}", Name, DeltaPatchURI!);
                finalDownloadUri = DeltaPatchURI ?? throw new ArgumentNullException(nameof(DeltaPatchURI), "Delta patch URI is null");
                finalZipPath = TargetDeltaPatchPath;
                deltaPatch = true;
            }

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(finalZipPath)!);
            } catch (Exception e) {
                UpdaterLogger.LogError(e, "Failed to create download directory at '{0}'", Path.GetDirectoryName(finalZipPath) ?? "--");
                throw;
            }

            IDownload download = DownloadBuilder.New()
                .WithConfiguration(configuration)
                .WithUrl(finalDownloadUri)
                .WithFileLocation(finalZipPath)
                .Build();

            TaskCompletionSource<bool> downloadCompleted = new TaskCompletionSource<bool>();
            download.DownloadFileCompleted += (sender, args) => {

                if (args.Cancelled) {
                    UpdaterLogger.LogWarn("Download of {0} was cancelled.", Name);
                    downloadCompleted.SetResult(false);
                    return;
                }

                if (args.Error != null) {
                    UpdaterLogger.LogError(args.Error, "Failed to download {0}", Name);
                    downloadCompleted.SetResult(false);
                    return;
                }

                UpdaterLogger.LogInfo("Download of {0} completed successfully.", Name);
                downloadCompleted.SetResult(true);
            };

            download.DownloadProgressChanged += DownloadProgressChanged;
            download.DownloadFileCompleted += DownloadFileCompleted;

            UpdaterLogger.LogInfo("Starting download of {0} from {1} - caching at {2}", Name, finalDownloadUri, finalZipPath);

            await download.StartAsync();

            bool result = await downloadCompleted.Task;
            if (!result) {
                throw new Exception("Failed to download resource.");
            }

            if (deltaPatch && !ApplyDeltaPatch()) {
                UpdaterLogger.LogWarn("Failed to apply delta patch to {0}", Name);

                Cleanup();
                await Retry();
            }


            if (!CheckDownload()) {
                UpdaterLogger.LogWarn("Checksum verification failed for {0}", Name);

                Cleanup();
                await Retry();
            }

            return TargetZipPath;
        }

        private async Task Retry() {
            if (RetryCount <= 0)
                throw new Exception("Failed to download resource.");

            RetryCount--;

            UpdaterLogger.LogInfo("Retrying download of {0}, {1} attempts left.", Name, RetryCount);
            await Download();
        }

        /// <summary>
        /// Apply patch to downloaded file:
        /// </summary>
        /// <exception cref="FileNotFoundException"></exception>
        private bool ApplyDeltaPatch() {
            if (DeltaSourceZipPath == null || !File.Exists(DeltaSourceZipPath))
                throw new FileNotFoundException("Source zip file not found.");
            if (!File.Exists(TargetDeltaPatchPath))
                throw new FileNotFoundException("Delta patch file not found.");

            UpdaterLogger.LogInfo("Applying delta patch to {0}", DeltaSourceZipPath);

            string intermediatePatchZipName = DeltaSourceZipPath + ".old";
            if (File.Exists(intermediatePatchZipName))
                File.Delete(intermediatePatchZipName);

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(intermediatePatchZipName)!);

                File.Move(DeltaSourceZipPath, intermediatePatchZipName);

                string finalZipPath = DeltaSourceZipPath;

                using (var basisStream = new FileStream(intermediatePatchZipName, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var deltaStream = new FileStream(TargetDeltaPatchPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var newFileStream = new FileStream(finalZipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read)) {
                    var deltaApplier = new DeltaApplier { SkipHashCheck = false };
                    deltaApplier.Apply(basisStream, new BinaryDeltaReader(deltaStream, new ConsoleProgressReporter()), newFileStream);
                }

                UpdaterLogger.LogDebug("Delta patch applied successfully to {0}", DeltaSourceZipPath);

                File.Delete(TargetDeltaPatchPath);
                File.Delete(intermediatePatchZipName);

                return true;
            } catch (Exception e) {
                UpdaterLogger.LogError(e, "Failed to apply delta patch to {0}", DeltaSourceZipPath);

                if (File.Exists(DeltaSourceZipPath))
                    File.Delete(DeltaSourceZipPath);
                if (File.Exists(intermediatePatchZipName))
                    File.Delete(intermediatePatchZipName);

                return false;
            }
        }

        private bool CheckDownload() {
            if (!File.Exists(TargetZipPath)) {
                UpdaterLogger.LogError(null, "Download of {0} failed.", Name);
                return false;
            }

            try {
                using ZipArchive z = ZipFile.OpenRead(TargetZipPath);
            } catch (Exception e) {
                UpdaterLogger.LogError(e, "Downloaded zip file {0} could not be opened correctly", Name);
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
                    UpdaterLogger.LogError(e, "Failed to verify checksum for {0}", Name);
                    return false;
                }
            } catch (Exception e) {
                // check if e is a http error with code 404
                if (e is AggregateException { InnerException: HttpRequestException }) {
                    UpdaterLogger.LogWarn("Checksum for {0} at {1} not found, skipping verification", Name, ChecksumURI);
                    return true;
                }

                UpdaterLogger.LogWarn("Failed to download checksum for {0}", Name);
                return true;
            }
        }

        private void Cleanup() {
            UpdaterLogger.LogDebug("Cleaning up failed download of {0}", Name);

            if (File.Exists(TargetZipPath))
                File.Delete(TargetZipPath);
            if (File.Exists(TargetDeltaPatchPath))
                File.Delete(TargetDeltaPatchPath);
        }
    }
}
