using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ForgeUpdater.Manifests {
    class NativeInterop {
        const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;

        [DllImport("kernel32.dll")]
        internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr FindResource(IntPtr hModule, string lpName, string lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);
    }

    public class ManifestReader<TManifest> where TManifest : Manifest {
        string file;

        bool IsEmbeddedManifest => !file.EndsWith(".json");

        public ManifestReader(string file) {
            this.file = file;
        }

        public TManifest? ReadManifest() {
            return ReadEmbeddedManifest();
        }

        public TManifest? ReadEmbeddedManifest() {
            UpdaterLogger.LogDebug("Searching for manifest in assembly: {0}", file);

            TManifest? manifest;
            if ((manifest = ReadEmbeddedManifestFromCLR()) != null) {
                return manifest;
            }

            if ((manifest = ReadEmbeddedManifestFromNative()) != null) {
                return manifest;
            }

            return null;
        }

        /// <summary>
        /// Tries to read an embedded manifest from a native assembly.
        /// 
        /// To embed a manifest in a native assembly, use the following resource script:
        /// <code>IDR_MANIFEST MANIFEST_FILE "manifest.json"</code>
        /// With the following defines:
        /// <code>
        /// #define MANIFEST_FILE ForgeManifest
        /// #define IDR_MANIFEST  Manifest
        /// </code>
        /// </summary>
        /// <returns>null, when no manifest was found in the dll, otherwise the manifest of that file</returns>
        private TManifest? ReadEmbeddedManifestFromNative() {
            UpdaterLogger.LogDebug("Trying to read native assembly for embedded manifest: {0}", file);

            IntPtr libraryHandle = NativeInterop.LoadLibraryEx(file, IntPtr.Zero, 0x00000002);
            if (libraryHandle == IntPtr.Zero) {
                UpdaterLogger.LogDebug("Failed to load library as data file: {0}", file);
                return null;
            }

            try {
                IntPtr resourceHandle = NativeInterop.FindResource(libraryHandle, "MANIFEST", "FORGEMANIFEST");
                if (resourceHandle == IntPtr.Zero) {
                    UpdaterLogger.LogDebug("No manifest resource found in: {0}", file);
                    return null;
                }

                IntPtr loadedResource = NativeInterop.LoadResource(libraryHandle, resourceHandle);
                if (loadedResource == IntPtr.Zero) {
                    UpdaterLogger.LogDebug("Failed to load manifest resource in: {0}", file);
                    return null;
                }

                IntPtr resourceData = NativeInterop.LockResource(loadedResource);
                if (resourceData == IntPtr.Zero) {
                    UpdaterLogger.LogDebug("Failed to lock manifest resource in: {0}", file);
                    return null;
                }

                uint resourceSize = NativeInterop.SizeofResource(libraryHandle, resourceHandle);
                byte[] manifestData = new byte[resourceSize];

                Marshal.Copy(resourceData, manifestData, 0, (int)resourceSize);
                TManifest? manifest = JsonSerializer.Deserialize<TManifest>(manifestData);
                if (manifest == null) {
                    UpdaterLogger.LogError(null, "Failed to parse manifest at {0}", file);
                    return null;
                }

                UpdaterLogger.LogDebug("Found native embedded manifest {0}@{1} in assembly {2}", manifest.Id, manifest.Version, file);
                manifest.Embedded = true;
                return manifest;
            } catch (JsonException e) {
                UpdaterLogger.LogError(e, "Failed to parse manifest at {0}", file);
                return null;
            } finally {
                NativeInterop.FreeLibrary(libraryHandle);
            }
        }

        private TManifest? ReadEmbeddedManifestFromCLR() {
            UpdaterLogger.LogDebug("Trying to read CLR assembly for embedded manifest: {0}", file);

            using Stream fileStream = File.OpenRead(file);
            using PEReader per = new PEReader(fileStream);

            MetadataReader mr;
            try {
                mr = per.GetMetadataReader();

            } catch (Exception) {
                UpdaterLogger.LogDebug("Not a dll with metadata in {0}", file);
                return null;
            }

            CorHeader? peHeadersCorHeader = per.PEHeaders.CorHeader;
            if (peHeadersCorHeader == null) {
                // Not a .NET assembly with manifest metadata
                UpdaterLogger.LogDebug("No .NET manifest data in {0}", file);
                return null;
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
                return null;
            }

            TManifest? manifest = default;
            try {
                manifest = JsonSerializer.Deserialize<TManifest>(manifestData);
            } catch (JsonException e) {
                UpdaterLogger.LogError(e, "Failed to parse manifest at {0}", file);
                return null;
            }

            if (manifest == null) {
                UpdaterLogger.LogError(null, "Failed to parse manifest at {0}", file);
                return null;
            }

            UpdaterLogger.LogDebug("Found CLR embedded manifest {0}@{1} in assembly {2}", manifest.Id, manifest.Version, file);

            manifest.Embedded = true;
            return manifest;
        }
    }
}
