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
        private readonly List<ManifestStore<TManifest>> remoteStore = new List<ManifestStore<TManifest>>();
        private readonly Store store;

        public Store Store => store;

        public StoreInstallation(string installationPath, string remoteStorePath) {
            store = new Store(installationPath, remoteStorePath);

            try {
                UpdatePipeline<TManifest>.CleanupLeftoverFiles(store.InstallationPath);
            } catch (Exception e) {
                UpdaterLogger.LogError(e, "Failed to clean residual files");
            }
        }

        public StoreInstallation(string storePath) {
            try {
                string fileContent = System.IO.File.ReadAllText(storePath);
                store = JsonSerializer.Deserialize<Store>(fileContent)!;

                if (!PathExtensions.IsPathFullyQualified(store.InstallationPath)) {
                    store.InstallationPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(storePath)!, store.InstallationPath));
                }

                for (int i = 0; i < store.RemoteStorePaths?.Length; i++) {
                    if (!PathExtensions.IsPathFullyQualified(store.RemoteStorePaths[i] ?? "")) {
                        store.RemoteStorePaths[i] = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(storePath)!, store.RemoteStorePaths[i]!));
                    }
                }
            } catch (Exception e) {
                UpdaterLogger.LogError(e, "Failed to read store configuration");
                throw;
            }
        }

        public async Task ReadLocalState() {
            localStore ??= await ManifestStore<TManifest>.CreateFromLocal(true, true, store.InstallationPath);
        }

        public async Task IngestRemoteStores(bool addDefaultRemote = false, params ManifestStore<TManifest>[] others) {
            if (addDefaultRemote && store.RemoteStorePaths != null)
                foreach (string remoteStorePath in store.RemoteStorePaths)
                    remoteStore.Add(await ManifestStore<TManifest>.Create(remoteStorePath));

            remoteStore.AddRange(others);
        }

        public async Task ReadStoreState() {
            await Task.WhenAll(ReadLocalState(), IngestRemoteStores(store.RemoteStorePaths != null));
        }

        public int LocalManifestCount => localStore?.Count ?? throw new InvalidOperationException("Store not initialized. Please run ReadStoreState first.");
        public int RemoteManifestCount => remoteStore?.Count ?? throw new InvalidOperationException("Store not initialized. Please run ReadStoreState first.");

        public IEnumerable<(TManifest? source, TManifest newer)> ManifestsToUpdate => localStore?.CheckForUpdatesWith(remoteStore.ToArray()) ?? throw new InvalidOperationException("Store not initialized. Please run ReadStoreState first.");
        public bool UpdateAvailable => localStore != null && remoteStore.Count != 0 && localStore.CheckForUpdatesWith(remoteStore.ToArray()).Any();

        public async IAsyncEnumerable<(TManifest target, string)> UpdateAll() {
            if (localStore == null || remoteStore.Count == 0) {
                throw new InvalidOperationException("Store not initialized. Please run ReadStoreState first.");
            }

            foreach ((TManifest? source, TManifest target) in localStore.CheckForUpdatesWith(remoteStore.ToArray())) {
                string installationPath = store.InstallationPath;

                if (store.InstallIntoFolders) {
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
