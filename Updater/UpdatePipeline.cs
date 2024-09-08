using ForgeUpdater.Manifests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ForgeUpdater.Updater {
    public class UpdatePipeline<TManifest> where TManifest : Manifest {
        public static async IAsyncEnumerable<(UpdatePipelineStep, float)> UpdateFromRemote(TManifest source, TManifest target, string installPath) {
            var downloader = new ResourceDownloader<TManifest>(source, target);

            var progress = Channel.CreateUnbounded<float>();
            downloader.DownloadProgressChanged += async (sender, e) => {
                await progress.Writer.WriteAsync((float)e.ProgressPercentage);
            };

            Task<string> downloadedZipTask = downloader.Download();
            while (!downloadedZipTask.IsCompleted) {
                await foreach (float progressPercentage in progress.Reader.ReadAllAsync()) {
                    yield return (UpdatePipelineStep.Download, progressPercentage);
                }
            }
            string downloadedZip = await downloadedZipTask;

            var updater = new ResourceUpdater<TManifest>(target, downloadedZip, installPath);
            foreach (float step in updater.Update()) {
                yield return (UpdatePipelineStep.Unpack, step * 100);
            }
        }

        public static async IAsyncEnumerable<(UpdatePipelineStep, float)> InstallFromRemote(TManifest target, string installPath) {
            var downloader = new ResourceDownloader<TManifest>(null, target);

            var progress = Channel.CreateUnbounded<float>();
            downloader.DownloadProgressChanged += async (sender, e) => {
                await progress.Writer.WriteAsync((float)e.ProgressPercentage);
            };

            Task<string> downloadedZipTask = downloader.Download();
            while (!downloadedZipTask.IsCompleted) {
                await foreach (float progressPercentage in progress.Reader.ReadAllAsync()) {
                    yield return (UpdatePipelineStep.Download, progressPercentage);
                }
            }
            string downloadedZip = await downloadedZipTask;

            var updater = new ResourceUpdater<TManifest>(target, downloadedZip, installPath);
            foreach (float step in updater.Update()) {
                yield return (UpdatePipelineStep.Unpack, step * 100);
            }
        }

        public static IEnumerable<(UpdatePipelineStep, float)> UpdateFromLocal(TManifest source, TManifest target, string zipPath, string installPath) {
            var updater = new ResourceUpdater<TManifest>(target, zipPath, installPath);
            foreach (float step in updater.Update()) {
                yield return (UpdatePipelineStep.Unpack, step * 100);
            }
        }

        public static IEnumerable<(UpdatePipelineStep, float)> InstallFromLocal(TManifest target, string zipPath, string installPath) {
            var updater = new ResourceUpdater<TManifest>(target, zipPath, installPath);
            foreach (float step in updater.Update()) {
                yield return (UpdatePipelineStep.Unpack, step * 100);
            }
        }
    }
    public enum UpdatePipelineStep {
        Download,
        Unpack
    }
}
