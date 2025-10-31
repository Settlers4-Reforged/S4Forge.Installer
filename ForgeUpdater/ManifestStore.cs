﻿using ForgeUpdater.Manifests;
using ForgeUpdater.Updater;

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

namespace ForgeUpdater {
    public class ManifestStore<TManifest> where TManifest : Manifest {
        private readonly List<TManifest> manifests = new List<TManifest>();

        public int Count => manifests.Count;

        public IEnumerable<TManifest> Manifests => manifests;

        public IEnumerable<(TManifest? source, TManifest newer)> CheckForUpdatesWith(params TManifest[]? remoteManifests) {
            if (remoteManifests == null) {
                yield break;
            }

            foreach (TManifest remoteManifest in remoteManifests) {
                TManifest? localManifest = manifests.Find(m => m.Id == remoteManifest.Id);

                // New entry in the remote store, "install" it:
                if (localManifest == null) {
                    yield return (null, remoteManifest);
                    continue;
                }

                if (remoteManifest.Version != localManifest.Version) {
                    yield return (localManifest, remoteManifest);
                }

                if (ResourceUpdater<TManifest>.IsInstallBroken(localManifest)) {
                    // If the local manifest is broken, we need to try again updating it to the remote version:
                    yield return (localManifest, remoteManifest);
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

        /// <summary>
        /// Will create Stores based on the provided URI type.
        /// <br/><br/>
        /// Types:
        /// <br/> 
        /// - Local Directory: "C:\path\to\manifests" (Runs with recursive:true, readEmbeddedManifests: true) <br/>
        /// - Store File: "C:/path/to/manifests.json" (local) or "https://example.com/manifests.json" (remote) <br/>
        /// </summary>
        public static async Task<ManifestStore<TManifest>> Create(string uri) {
            return uri switch {
                _ when uri.StartsWith("http") => await CreateFromRemoteStore(uri),
                _ when uri.EndsWith(".json") => await CreateFromLocalStore(uri),
                _ => await CreateFromLocal(true, true, uri)
            };
        }

        public static async Task<ManifestStore<TManifest>> CreateFromLocal(bool recursive, bool readEmbeddedManifests, params string[] paths) {
            ManifestStore<TManifest> store = new ManifestStore<TManifest>();
            return await store.FromLocal(recursive, readEmbeddedManifests, paths);
        }

        static int GetRecursionDepth(string basePath, string targetPath) {
            // Normalize and get absolute paths
            basePath = Path.GetFullPath(basePath.TrimEnd(Path.DirectorySeparatorChar));
            targetPath = Path.GetFullPath(targetPath.TrimEnd(Path.DirectorySeparatorChar));

            if (!targetPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Target path is not a subpath of the base path.");

            // Count directories
            int baseDepth = basePath.Split(Path.DirectorySeparatorChar).Length;
            int targetDepth = targetPath.Split(Path.DirectorySeparatorChar).Length;

            return targetDepth - baseDepth;
        }

        public async Task<ManifestStore<TManifest>> FromLocal(bool recursive, bool readEmbeddedManifests, params string[] paths) {
            foreach (string path in paths) {
                foreach (string file in Directory.GetFiles(path, "manifest.json", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)) {
                    int depth = GetRecursionDepth(path, file);
                    if (depth > 2) {
                        UpdaterLogger.LogDebug("Skipping manifest in subdirectory: {0}, depth {1} > 2", file, depth);
                        continue;
                    }

                    try {
                        using Stream fileStream = File.OpenRead(file);
                        TManifest? manifest = await JsonSerializer.DeserializeAsync<TManifest>(fileStream);

                        if (manifest == null) {
                            UpdaterLogger.LogError(null, "Failed to parse manifest at {0}", file);
                            continue;
                        }

                        UpdaterLogger.LogDebug("Found manifest: {0}@{1} in {2}", manifest.Id, manifest.Version, Path.GetDirectoryName(file)!);

                        manifest.ManifestPath = file;
                        manifests.Add(manifest);
                    } catch (JsonException e) {
                        UpdaterLogger.LogError(e, "Failed to parse manifest at {0}", file);
                    } catch (Exception e) {
                        UpdaterLogger.LogError(e, "Failed to read manifest at {0}", file);
                    }
                }

                if (!readEmbeddedManifests)
                    continue;

                var dllFiles = Directory.GetFiles(path, "*.dll", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                var asiFiles = Directory.GetFiles(path, "*.asi", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                foreach (string file in dllFiles.Concat(asiFiles)) {
                    int depth = GetRecursionDepth(path, file);
                    if (depth > 2) {
                        UpdaterLogger.LogDebug("Skipping assembly in subdirectory: {0}, depth {1} > 2", file, depth);
                        continue;
                    }

                    try {
                        UpdaterLogger.LogDebug("Searching for manifest in assembly: {0}", file);

                        using Stream fileStream = File.OpenRead(file);

                        using PEReader per = new PEReader(fileStream);
                        MetadataReader mr;
                        try {
                            mr = per.GetMetadataReader();

                        } catch (Exception) {
                            UpdaterLogger.LogDebug("Not a dll with metadata in {0}", file);
                            continue;
                        }

                        CorHeader? peHeadersCorHeader = per.PEHeaders.CorHeader;
                        if (peHeadersCorHeader == null) {
                            // Not a .NET assembly with manifest metadata
                            UpdaterLogger.LogDebug("No .NET manifest data in {0}", file);
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
                            UpdaterLogger.LogDebug("No manifest found in {0}", file);
                            continue;
                        }

                        TManifest? manifest = JsonSerializer.Deserialize<TManifest>(manifestData);

                        if (manifest == null) {
                            UpdaterLogger.LogError(null, "Failed to parse manifest at {0}", file);
                            continue;
                        }

                        UpdaterLogger.LogDebug("Found manifest {0}@{1} in assembly {2}", manifest.Id, manifest.Version, file);

                        manifest.Embedded = true;
                        manifests.Add(manifest);
                    } catch (JsonException e) {
                        UpdaterLogger.LogError(e, "Failed to parse manifest at {0}", file);
                    } catch (Exception e) {
                        UpdaterLogger.LogError(e, "Failed to read manifest at {0}", file);
                    }
                }
            }

            return this;
        }

        public static async Task<ManifestStore<TManifest>> CreateFromRemoteStore(params string[] remotes) {
            using HttpClient httpClient = new HttpClient();
            return await CreateFromRemoteStore(httpClient, remotes);
        }

        public static async Task<ManifestStore<TManifest>> CreateFromRemoteStore(HttpClient client, params string[] remotes) {
            List<string> stores = new List<string>();
            foreach (string remote in remotes) {
                try {
                    using HttpResponseMessage response = await client.GetAsync(remote);
                    response.EnsureSuccessStatusCode();

                    using Stream stream = await response.Content.ReadAsStreamAsync();
                    stores.Add(await new StreamReader(stream).ReadToEndAsync());
                } catch (Exception e) {
                    UpdaterLogger.LogError(e, "Failed to read remote manifests from {0}", remote);
                }
            }


            ManifestStore<TManifest> store = new ManifestStore<TManifest>();
            return store.FromStore(stores.ToArray());
        }

        public static async Task<ManifestStore<TManifest>> CreateFromLocalStore(string path) {
            string storeJson = File.ReadAllText(path);
            ManifestStore<TManifest> store = new ManifestStore<TManifest>();
            return store.FromStore(storeJson);
        }


        public ManifestStore<TManifest> FromStore(params string[] storeJson) {
            manifests.Clear();

            foreach (string store in storeJson) {
                try {
                    TManifest[]? newManifests = JsonSerializer.Deserialize<TManifest[]>(store);

                    if (newManifests == null) {
                        UpdaterLogger.LogError(null, "Failed to parse remote manifests\n{0}", store);
                        continue;
                    }

                    manifests.AddRange(newManifests);
                } catch (JsonException e) {
                    UpdaterLogger.LogError(e, "Failed to parse remote manifests\n{0}", store);
                } catch (Exception e) {
                    UpdaterLogger.LogError(e, "Failed to read remote manifests\n{0}", store);
                }
            }

            return this;
        }
    }
}
