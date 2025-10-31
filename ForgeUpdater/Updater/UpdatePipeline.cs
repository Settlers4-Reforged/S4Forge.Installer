using ForgeUpdater.Manifests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ForgeUpdater.Updater {
    public class UpdatePipeline<TManifest> where TManifest : Manifest {
        public static void CleanupLeftoverFiles(string installPath) {
            ResourceUpdater<TManifest>.CleanupLeftoverFiles(installPath);
        }

        public static IAsyncEnumerable<string> Update(TManifest? source, TManifest target, string installPath) {
            if (target.Assets == null) {
                throw new InvalidOperationException("Manifest does not have a download URL.");
            }

            bool isRemote = target.Assets.AssetURI.StartsWith("http");

            try {
                return isRemote ? UpdateFromRemote(source, target, installPath) : UpdateFromLocal(source, target, installPath);
            } catch (Exception e) {
                UpdaterLogger.LogError(e, "Failed to update resource");
            }

            return AsyncEnumerable.Empty<string>();
        }

        public static async IAsyncEnumerable<string> UpdateFromRemote(TManifest? source, TManifest target, string installPath) {
            if (target.Assets == null) {
                throw new InvalidOperationException("Manifest does not have a download URL.");
            }
            if (target.Assets == null) {
                throw new InvalidOperationException("Manifest does not have a download URL.");
            }

            var downloader = new ResourceDownloader<TManifest>(source, target);

            var progress = Channel.CreateUnbounded<float>();
            downloader.DownloadProgressChanged += async (sender, e) => {
                await progress.Writer.WriteAsync((float)e.ProgressPercentage / 100f);
            };

            Task<string> downloadedZipTask = downloader.Download();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            downloadedZipTask.ContinueWith((_) => {
                progress.Writer.Complete();
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            while (!downloadedZipTask.IsCompleted) {
                float progressPercentage = 1;

                try {
                    progressPercentage = await progress.Reader.ReadAsync();
                } catch (ChannelClosedException) { }

                yield return $"Download: {progressPercentage:P}";
            }
            string downloadedZip = await downloadedZipTask;

            var updater = new ResourceUpdater<TManifest>(target, downloadedZip, installPath);
            foreach (float step in updater.Update()) {
                yield return $"Unpack: {step:P}";
            }

            yield return "Done";
        }

        public static async IAsyncEnumerable<string> UpdateFromLocal(TManifest? source, TManifest target, string installPath, string? zipPath = null) {
            if (target.Assets == null) {
                throw new InvalidOperationException("Manifest does not have a download URL.");
            }

            var updater = new ResourceUpdater<TManifest>(target, zipPath ?? target.Assets.AssetURI, installPath);
            foreach (float step in updater.Update()) {
                yield return $"Unpack: {step:P}";
            }
        }
    }
    public enum UpdatePipelineStep {
        Download,
        Unpack
    }
}
