using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ForgeUpdater.Manifests {
    public record ManifestDownload {
        [JsonPropertyName("uri")]
        [JsonRequired]
        public string AssetURI { get; set; }

        /// <summary>
        /// All available delta patches for this resource.
        /// Source is the file name of the base version zip, delta is URI to the patch to the next version.
        /// </summary>
        [JsonPropertyName("deltaPatchesURI")]
        public Dictionary<ManifestVersion, DeltaPatch>? DeltaPatchesURI { get; set; }

        public record DeltaPatch {
            [JsonPropertyName("sourceFileName")]
            [JsonRequired]
            public string SourceFileName { get; set; }
            [JsonPropertyName("deltaURI")]
            [JsonRequired]
            public string DeltaURI { get; set; }

            // implicit convert from Tuple
            public static implicit operator DeltaPatch((string SourceFileName, string DeltaURI) tuple) => new DeltaPatch() {
                SourceFileName = tuple.SourceFileName,
                DeltaURI = tuple.DeltaURI
            };

            // implicit convert to Tuple
            public static implicit operator (string SourceFileName, string DeltaURI)(DeltaPatch deltaPatch) => (deltaPatch.SourceFileName, deltaPatch.DeltaURI);
        }

        public (string SourceFileName, string DeltaURI)? DeltaPatchUrlFromVersion(string? version) {
            if (DeltaPatchesURI == null || version == null) {
                return null;
            }

            ManifestVersion currentVersion = new ManifestVersion(version);
            ManifestVersion latestVersion = DeltaPatchesURI.Keys.OrderByDescending(v => v).First();

            if (currentVersion == latestVersion) {
                return null;
            }

            if (!DeltaPatchesURI.ContainsKey(currentVersion)) {
                return null;
            }

            return DeltaPatchesURI[currentVersion];
        }

        public virtual bool Equals(ManifestDownload? other) {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            if (AssetURI != other.AssetURI)
                return false;
            if (DeltaPatchesURI == null && other.DeltaPatchesURI == null)
                return true;
            if (DeltaPatchesURI == null || other.DeltaPatchesURI == null)
                return false;
            if (DeltaPatchesURI.Count != other.DeltaPatchesURI.Count)
                return false;

            foreach (var kvp in DeltaPatchesURI) {
                if (!other.DeltaPatchesURI.ContainsKey(kvp.Key))
                    return false;

                var otherDeltaPatch = other.DeltaPatchesURI[kvp.Key];
                var deltaPatch = kvp.Value;

                if (deltaPatch.SourceFileName != otherDeltaPatch.SourceFileName || deltaPatch.DeltaURI != otherDeltaPatch.DeltaURI)
                    return false;
            }

            return true;
        }
    }
}
