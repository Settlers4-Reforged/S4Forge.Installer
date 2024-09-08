using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ForgeUpdater.Manifests;

public record Manifest {
    public const string ManifestFileName = "manifest.json";

    /// <summary>
    /// The name of the resource.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// The id of the resource.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// The URL of the resource's documentation.
    /// <br/>
    /// It should point to a "folder" of where to find the requested resources.
    /// <br/><br/>
    /// <b>Example:</b> `https://example.com/assets/` and the resource "X" at version 1.0.0 is then located at `https://example.com/assets/X.1.0.0.zip/`
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// The URL of the manifest.
    /// </summary>
    [JsonPropertyName("manifest")]
    public string? ManifestUrl { get; set; }

    /// <summary>
    /// A list of files or folders that should be ignored when updating/deleting/verifying this resource.
    /// </summary>
    public string[]? IgnoredEntries { get; set; }

    /// <summary>
    /// Whether or not the updater should clear residual files after an update.
    /// E.g. if the update should remove files that are no longer needed (Which are also not ignored in the manifest).
    /// </summary>
    public bool ClearResidualFiles { get; set; } = UpdaterConfig.DefaultUpdateShouldClearResidualFiles;

    /// <summary>
    /// The version of the manifest.
    /// </summary>
    /// <remarks>
    /// Required format: `Major.Minor.Patch` (e.g. `1.0.0`)
    /// </remarks>
    [JsonPropertyName("version")]
    public required ManifestVersion Version { get; set; }

    /// <summary>
    /// The type of the resource.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// A list of dependencies that this plugin requires to function.
    /// </summary>
    [JsonPropertyName("relationships")]
    public Relationship[] Relationships { get; set; } = Array.Empty<Relationship>();

}
