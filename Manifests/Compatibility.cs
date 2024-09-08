using System.Text.Json.Serialization;

namespace ForgeUpdater.Manifests;

public record Compatibility {
    /// <summary>
    /// Gets or sets the minimum compatibility version.
    /// </summary>
    [JsonPropertyName("minimum")]
    public required ManifestVersion Minimum { get; set; }

    /// <summary>
    /// Gets or sets the verified compatibility version.
    /// </summary>
    [JsonPropertyName("verified")]
    public ManifestVersion? Verified { get; set; }

    /// <summary>
    /// Gets or sets the maximum compatibility version.
    /// </summary>
    [JsonPropertyName("maximum")]
    public ManifestVersion? Maximum { get; set; }


    public CompatibilityLevel CheckCompatibility(ManifestVersion other) {
        if (Verified != null && Verified.CompareTo(other) == 0) {
            return CompatibilityLevel.Verified;
        }

        if (Minimum.CompareTo(other) > 0) {
            return CompatibilityLevel.IncompatibleUnder;
        }

        if (Maximum != null && Maximum.CompareTo(other) < 0) {
            return CompatibilityLevel.IncompatibleOver;
        }

        return CompatibilityLevel.Compatible;
    }
}
