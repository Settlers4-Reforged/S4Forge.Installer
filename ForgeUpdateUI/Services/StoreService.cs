﻿using ForgeUpdater;
using ForgeUpdater.Manifests;

using ForgeUpdateUI.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ForgeUpdateUI.Services {
    public class StoreService {
        public List<StoreInstallation<Manifest>> Stores { get; } = new List<StoreInstallation<Manifest>>();

        private LoggerService loggerService;

        public StoreService(LoggerService loggerService) {
            this.loggerService = loggerService;

            string[] args = Program.CommandLineArgs;
            List<string> stores = (from arg in args where arg.StartsWith("--store=") select arg.Substring(8).Trim('\'')).ToList();
            if (stores.Count == 0) {
                Console.WriteLine("Usage: ForgeUpdateUI --store='<store_path>'");
                return;
            }

            stores.AddRange(from arg in args where arg.StartsWith("--storeDir=") from file in Directory.GetFiles(arg.Substring(11)) where file.EndsWith(".json") select file);

            if (stores.Count == 0) {
                Console.WriteLine("Usage: ForgeUpdateUI --store='<store_path>'");
                return;
            }

            foreach (string storePath in stores) {
                loggerService.LogInfo($"Loading store installations from {storePath}");
                // TODO: Add way to use remote installation path
                if (!File.Exists(storePath)) {
                    throw new FileNotFoundException("Store file not found", storePath);
                }

                string fileContent = File.ReadAllText(storePath);
                Installation[] installations;
                try {
                    installations = JsonSerializer.Deserialize<Installation[]>(fileContent)!;
                } catch (JsonException e) {
                    // File is probably just a single installation
                    installations = [JsonSerializer.Deserialize<Installation>(fileContent)!];
                }

                foreach (var installation in installations) {
                    if (installation == null) {
                        loggerService.LogError(null, "Failed to parse installation from store file {0}", storePath);
                        continue;
                    }

                    StoreInstallation<Manifest> storeInstallation = new StoreInstallation<Manifest>(installation, storePath);
                    Stores.Add(storeInstallation);
                }
            }
        }

        public async Task ReadStoreState() {
            await Task.WhenAll(Stores.Select(store => {
                loggerService.LogInfo("#### Reading Store state for '{0}' ####", store.Installation.Name);
                return store.ReadStoreState();
            }));
        }

        public async Task Update(Action<List<UpdateItem>>? onUpdate = null) {
            List<UpdateItem> values = new List<UpdateItem>();

            await ReadStoreState();
            values.AddRange(this.Stores.SelectMany(store => store.LocalManifests).Where(s => s != null).Select(manifest => new UpdateItem() {
                Name = manifest!.Name,
                Progress = "",
                Version = manifest.Version.ToString()
            }));

            onUpdate?.Invoke(values);

            var list = this.UpdateAll();

            await foreach (var update in list) {
                values.Add(update);
                onUpdate?.Invoke(values);
            }
        }

        public async IAsyncEnumerable<UpdateItem> UpdateAll() {
            foreach (var store in Stores) {
                var updates = store.ManifestsToUpdate.ToArray();

                if (updates.Length == 0) {
                    loggerService.LogInfo("No updates available for Store '{0}'", store.Installation.Name);
                }

                Manifest? source = null;
                string patchString = "";
                await foreach (var update in store.UpdateAll()) {
                    if (source == null || source.Id != update.target.Id) {
                        source = updates.First(u => u.newer.Id == update.target.Id).source;
                        if (source == null) {
                            source = update.target;
                            patchString = $"->{update.target.Version}";
                        } else {
                            patchString = $"{source.Version}->{update.target.Version}";
                        }
                    }

                    yield return new UpdateItem() {
                        Name = update.target.Name,
                        Version = patchString,
                        Progress = update.Item2
                    };

                    await Task.Delay(1000);
                }
            }
        }
    }
}
