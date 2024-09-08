using ForgeUpdater.Manifests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ForgeUpdater.Updater {
    public class UpdatePipeline<TManifest> where TManifest : Manifest {
        public static IAsyncEnumerable<(UpdatePipelineStep, float)> UpdateFromRemote(TManifest source, TManifest target, string installPath) {
            return InstallFromRemote(target, installPath);
        }

        public static async IAsyncEnumerable<(UpdatePipelineStep, float)> InstallFromRemote(TManifest target, string installPath) {
            var downloader = new ResourceDownloader<TManifest>(null, target);

            var progress = Channel.CreateUnbounded<float>();
            downloader.DownloadProgressChanged += async (sender, e) => {
                await progress.Writer.WriteAsync((float)e.ProgressPercentage / 100f);
            };

            Task<string> downloadedZipTask = downloader.Download();

            CancellationTokenSource cts = new CancellationTokenSource();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            downloadedZipTask.ContinueWith((_) => {
                cts.Cancel();
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            while (!downloadedZipTask.IsCompleted) {
                float progressPercentage = 1;

                try {
                    progressPercentage = await progress.Reader.ReadAsync(cts.Token);
                } catch (OperationCanceledException) { }

                yield return (UpdatePipelineStep.Download, progressPercentage);
            }
            string downloadedZip = await downloadedZipTask;

            var updater = new ResourceUpdater<TManifest>(target, downloadedZip, installPath);
            foreach (float step in updater.Update()) {
                yield return (UpdatePipelineStep.Unpack, step);
            }
        }

        public static IEnumerable<(UpdatePipelineStep, float)> UpdateFromLocal(TManifest source, TManifest target, string zipPath, string installPath) {
            return InstallFromLocal(target, zipPath, installPath);
        }

        public static IEnumerable<(UpdatePipelineStep, float)> InstallFromLocal(TManifest target, string zipPath, string installPath) {
            var updater = new ResourceUpdater<TManifest>(target, zipPath, installPath);
            foreach (float step in updater.Update()) {
                yield return (UpdatePipelineStep.Unpack, step);
            }
        }
    }
    public enum UpdatePipelineStep {
        Download,
        Unpack
    }
}
