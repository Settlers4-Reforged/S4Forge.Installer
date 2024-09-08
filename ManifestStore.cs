using ForgeUpdater.Manifests;

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

namespace ForgeUpdater {
    public class ManifestStore<TManifest> where TManifest : Manifest {
        private readonly List<TManifest> manifests = new List<TManifest>();

        public IEnumerable<(TManifest source, TManifest newer)> CheckForUpdatesWith(params ManifestStore<TManifest>[] other) {
            List<TManifest> remoteManifests = other.SelectMany(m => m.manifests).ToList();

            foreach (TManifest manifest in manifests) {
                TManifest? otherManifest = remoteManifests.Find(m => m.Id == manifest.Id);

                if (otherManifest == null) {
                    continue;
                }

                if (manifest.Version != otherManifest.Version) {
                    yield return (manifest, otherManifest);
                }
            }
        }

        public IEnumerable<(TManifest source, Relationship incompatibility, CompatibilityLevel level)> CheckForIncompatibilities() {
            foreach (TManifest manifest in manifests) {
                foreach (Relationship relationship in manifest.Relationships) {
                    // Find the relationship in the current store:
                    TManifest? otherManifest = manifests.Find(m => m.Id == relationship.Id);

                    // Not found!
                    if (otherManifest == null) {
                        if (relationship.Optional == true) {
                            continue;
                        }

                        // Report relationship as missing:
                        yield return (manifest, relationship, CompatibilityLevel.Unknown);
                        continue;
                    }


                    // Check if the relationship is compatible:
                    CompatibilityLevel level = relationship.Compatibility.CheckCompatibility(otherManifest.Version);
                    if (level < CompatibilityLevel.Compatible) {
                        yield return (manifest, relationship, level);
                    }
                }
            }
        }

        public void Add(TManifest manifest) {
            manifests.Add(manifest);
        }

        public void AddRange(IEnumerable<TManifest> manifests) {
            this.manifests.AddRange(manifests);
        }

        protected void MergeDuplicates() {
            var newManifests = manifests.GroupBy(m => m.Id)
                .Select(g => g.OrderByDescending(m => m.Version).First())
                .ToList();

            manifests.Clear();
            manifests.AddRange(newManifests);
        }

        public static async Task<ManifestStore<TManifest>> FromLocal(bool recursive, bool readEmbeddedManifests, params string[] paths) {
            ManifestStore<TManifest> store = new ManifestStore<TManifest>();

            foreach (string path in paths) {
                foreach (string file in Directory.GetFiles(path, "manifest.json", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)) {
                    try {
                        await using Stream fileStream = File.OpenRead(file);
                        TManifest? manifest = await JsonSerializer.DeserializeAsync<TManifest>(fileStream);

                        if (manifest == null) {
                            UpdaterLogger.LogError(null, "Failed to parse manifest at %s", file);
                            continue;
                        }

                        store.manifests.Add(manifest);
                    } catch (JsonException e) {
                        UpdaterLogger.LogError(e, "Failed to parse manifest at %s", file);
                    } catch (Exception e) {
                        UpdaterLogger.LogError(e, "Failed to read manifest at %s", file);
                    }
                }

                if (!readEmbeddedManifests)
                    continue;

                foreach (string file in Directory.GetFiles(path, "*.dll", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)) {
                    try {
                        UpdaterLogger.LogDebug("Trying to read manifest from %s", file);

                        await using Stream fileStream = File.OpenRead(file);

                        PEReader per = new PEReader(fileStream);
                        MetadataReader mr = per.GetMetadataReader();
                        CorHeader? peHeadersCorHeader = per.PEHeaders.CorHeader;
                        if (peHeadersCorHeader == null) {
                            // Not a .NET assembly with manifest metadata
                            UpdaterLogger.LogDebug("No .NET manifest data in %s", file);
                            continue;
                        }

                        byte[]? manifestData = null;
                        foreach (var resHandle in mr.ManifestResources) {
                            ManifestResource res = mr.GetManifestResource(resHandle);
                            if (!mr.StringComparer.Equals(res.Name, "manifest.json"))
                                continue;

                            PEMemoryBlock resourceDirectory = per.GetSectionData(peHeadersCorHeader.ResourcesDirectory.RelativeVirtualAddress);
                            BlobReader reader = resourceDirectory.GetReader(
                                (int)res.Offset,
                                resourceDirectory.Length - (int)res.Offset);

                            uint size = reader.ReadUInt32();
                            manifestData = reader.ReadBytes((int)size);
                            break;
                        }

                        if (manifestData == null) {
                            UpdaterLogger.LogDebug("No manifest found in %s", file);
                            continue;
                        }

                        TManifest? manifest = JsonSerializer.Deserialize<TManifest>(manifestData);

                        if (manifest == null) {
                            UpdaterLogger.LogError(null, "Failed to parse manifest at %s", file);
                            continue;
                        }

                        store.manifests.Add(manifest);
                    } catch (JsonException e) {
                        UpdaterLogger.LogError(e, "Failed to parse manifest at %s", file);
                    } catch (Exception e) {
                        UpdaterLogger.LogError(e, "Failed to read manifest at %s", file);
                    }
                }
            }

            return store;
        }

        public static async Task<ManifestStore<TManifest>> FromRemote(params string[] remotes) {
            using HttpClient httpClient = new HttpClient();
            return await FromRemote(httpClient, remotes);
        }

        public static async Task<ManifestStore<TManifest>> FromRemote(HttpClient client, params string[] remotes) {
            ManifestStore<TManifest> store = new ManifestStore<TManifest>();

            foreach (string remote in remotes) {
                try {
                    using HttpResponseMessage response = await client.GetAsync(remote);

                    if (!response.IsSuccessStatusCode) {
                        UpdaterLogger.LogError(null, "Failed to fetch manifest from %s: %s", remote, response.ReasonPhrase ?? "");
                        continue;
                    }

                    await using Stream stream = await response.Content.ReadAsStreamAsync();
                    TManifest[]? remoteManifests = await JsonSerializer.DeserializeAsync<TManifest[]>(stream);

                    if (remoteManifests == null) {
                        UpdaterLogger.LogError(null, "Failed to parse manifest from %s", remote);
                        continue;
                    }

                    store.manifests.AddRange(remoteManifests);
                } catch (HttpRequestException e) {
                    UpdaterLogger.LogError(e, "Failed to fetch manifest from %s", remote);
                } catch (JsonException e) {
                    UpdaterLogger.LogError(e, "Failed to parse manifest from %s", remote);
                } catch (Exception e) {
                    UpdaterLogger.LogError(e, "Failed to fetch manifest from %s", remote);
                }
            }

            store.MergeDuplicates();
            return store;
        }
    }
}
