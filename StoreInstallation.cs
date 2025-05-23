using ForgeUpdater.Manifests;
using ForgeUpdater.Updater;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ForgeUpdater {
    public class StoreInstallation<TManifest> where TManifest : Manifest {
        private ManifestStore<TManifest>? localStore;
        private List<TManifest>? feedManifests;
        private readonly Installation installation;

        public Installation Installation => installation;

        public StoreInstallation(string storePath) {
            try {

                // TODO: Add way to use remote installation path
                if (!File.Exists(storePath)) {
                    throw new FileNotFoundException("Store file not found", storePath);
                }

                string fileContent = File.ReadAllText(storePath);
                installation = JsonSerializer.Deserialize<Installation>(fileContent)!;

                if (!installation.InstallationPath.IsPathAbsolute()) {
                    installation.InstallationPath = Path.GetFullPath(Path.Combine(UpdaterConfig.WorkingDirectory, installation.InstallationPath));
                }

                if (Directory.Exists(installation.InstallationPath)) {
                    installation.InstallationPath = Path.GetFullPath(installation.InstallationPath);
                } else {
                    UpdaterLogger.LogWarn("Installation path does not exist: {0}", installation.InstallationPath);
                    try {
                        Directory.CreateDirectory(installation.InstallationPath);
                    } catch (Exception e) {
                        UpdaterLogger.LogError(e, "Failed to create installation path: {0}", installation.InstallationPath);
                        throw;
                    }
                }


                if (installation.ManifestFeeds == null) {
                    UpdaterLogger.LogError(null, "No manifest feeds found in store {0}", installation.Name);
                    return;
                }

                for (int i = 0; i < installation.ManifestFeeds?.Length; i++) {
                    Installation.ManifestFeed feed = installation.ManifestFeeds[i];
                    if (feed.ManifestUri == null) {
                        UpdaterLogger.LogError(null, "Manifest feed in store {0} is null at index {1}", storePath, i);
                        continue;
                    }

                    if (!feed.ManifestUri.IsPathAbsolute()) {
                        feed.ManifestUri = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(storePath)!, feed.ManifestUri));
                    }
                }
            } catch (Exception e) {
                UpdaterLogger.LogError(e, "Failed to read store configuration");
                throw;
            }
        }

        public async Task ReadLocalState() {
            UpdaterLogger.LogInfo("Reading local manifests from {0}", installation.InstallationPath);
            localStore ??= await ManifestStore<TManifest>.CreateFromLocal(true, true, installation.InstallationPath);

            UpdaterLogger.LogDebug("Read {0} local manifests from {1}", localStore.Count, installation.InstallationPath);
        }

        public async Task IngestRemoteFeeds() {
            feedManifests ??= new List<TManifest>();

            UpdaterLogger.LogInfo("Ingesting {0} remote feeds", installation.ManifestFeeds?.Length ?? 0);

            foreach (Uri manifestUri in installation.ManifestFeeds!.Select(feed => new Uri(feed.ManifestUri))) {
                TManifest? manifest;
                try {
                    UpdaterLogger.LogDebug("Reading manifest from {0}", manifestUri);

                    if (manifestUri.IsFile) {
                        using FileStream manifestStream = File.OpenRead(manifestUri.LocalPath);
                        manifest = await JsonSerializer.DeserializeAsync<TManifest>(manifestStream);
                    } else {
                        using HttpClient client = new HttpClient();
                        using Stream manifestStream = await client.GetStreamAsync(manifestUri);
                        manifest = await JsonSerializer.DeserializeAsync<TManifest>(manifestStream);
                    }

                    if (manifest == null) {
                        throw new InvalidOperationException("Manifest is null");
                    }

                    UpdaterLogger.LogInfo("Read manifest {0}@{1} from {2}", manifest.Id, manifest.Version, manifestUri);
                } catch (Exception e) {
                    UpdaterLogger.LogError(e, "Failed to read manifest from {0}", manifestUri);
                    continue;
                }

                feedManifests.Add(manifest);
            }
        }

        public async Task ReadStoreState() {
            await Task.WhenAll(ReadLocalState(), IngestRemoteFeeds());
        }

        public int LocalManifestCount => localStore?.Count ?? throw new InvalidOperationException("Store not initialized. Please run ReadStoreState first.");
        public int RemoteManifestCount => feedManifests?.Count ?? throw new InvalidOperationException("Store not initialized. Please run ReadStoreState first.");

        public IEnumerable<TManifest> LocalManifests => localStore?.Manifests ?? throw new InvalidOperationException("Store not initialized. Please run ReadStoreState first.");
        public IEnumerable<(TManifest? source, TManifest newer)> ManifestsToUpdate => localStore?.CheckForUpdatesWith(feedManifests?.ToArray()) ?? throw new InvalidOperationException("Store not initialized. Please run ReadStoreState first.");
        public bool UpdateAvailable => localStore != null && feedManifests?.Count != 0 && localStore.CheckForUpdatesWith(feedManifests?.ToArray()).Any();

        public async IAsyncEnumerable<(TManifest target, string)> UpdateAll() {
            if (localStore == null || feedManifests == null) {
                throw new InvalidOperationException("Store not initialized. Please run ReadStoreState first.");
            }

            foreach ((TManifest? source, TManifest target) in localStore.CheckForUpdatesWith([.. feedManifests])) {
                string installationPath = installation.InstallationPath;

                if (installation.InstallIntoFolders) {
                    installationPath = Path.Combine(installationPath, target.Id);
                }

                if (source == null) {
                    UpdaterLogger.LogInfo("Installing {0} into {1}", target.Name, installationPath);
                } else {
                    UpdaterLogger.LogInfo("Updating {0} from {1} to {2} in {3}", target.Name, source.Version, target.Version, installationPath);
                }

                await foreach (string progress in UpdatePipeline<TManifest>.Update(source, target, installationPath)) {
                    yield return (target, progress);
                }

                try {
                    if (source != null) {
                        source?.UpdateFile(target);
                    } else if (!target.Embedded) {
                        string newManifestPath = Path.Combine(installationPath, "manifest.json");
                        target.Save(newManifestPath);
                    }
                } catch (Exception e) {
                    UpdaterLogger.LogError(e, "Failed to update manifest file: %s", target.Name);
                }

                yield return (target, "Done");
            }
        }
    }
}
