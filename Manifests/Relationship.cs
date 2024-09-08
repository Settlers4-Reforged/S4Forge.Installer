using System.Text.Json.Serialization;

namespace ForgeUpdater.Manifests;

public record Relationship {
    /// <summary>
    /// The id of the dependency.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Whether the dependency is optional, or not.
    /// </summary>
    [JsonPropertyName("optional")]
    public bool? Optional { get; set; }

    /// <summary>
    /// An explicit manifest url to be used for downloading the dependency. If a manifest is not provided, the dependency package must exist in the loaded store.
    /// </summary>
    [JsonPropertyName("manifest")]
    public string? ManifestUrl { get; set; }

    /// <summary>
    /// The compatible versions for the dependency. E.g. what version the relationship should have for it to be compatible.
    /// </summary>
    [JsonPropertyName("compatibility")]
    public required Compatibility Compatibility { get; set; }
}
