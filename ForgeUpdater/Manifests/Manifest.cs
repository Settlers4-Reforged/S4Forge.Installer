﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ForgeUpdater.Manifests;

public record Manifest {
    public const string ManifestFileName = "manifest.json";

    /// <summary>
    /// The name of the resource.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonRequired]
    public string Name { get; set; }

    /// <summary>
    /// The id of the resource.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonRequired]
    public string Id { get; set; }

    /// <summary>
    /// The URI of the resource's documentation.
    /// <br/>
    /// It should point to the zip of the resource.
    /// <br/><br/>
    /// <b>Example:</b> `https://example.com/assets/X.1.0.0.zip/` or `C:\Users\example\Downloads\X.1.0.0.zip`
    /// </summary>
    [JsonPropertyName("assets")]
    public ManifestDownload? Assets { get; set; }

    /// <summary>
    /// A list of files or folders that should be ignored when updating/deleting/verifying this resource.
    /// </summary>
    [JsonPropertyName("ignoredEntries")]
    public string[]? IgnoredEntries { get; set; }

    /// <summary>
    /// Whether or not the updater should clear residual files after an update.
    /// E.g. if the update should remove files that are no longer needed (Which are also not ignored in the manifest).
    /// </summary>
    [JsonPropertyName("clearResidualFiles")]
    public bool ClearResidualFiles { get; set; } = UpdaterConfig.DefaultUpdateShouldClearResidualFiles;

    /// <summary>
    /// The version of the manifest.
    /// </summary>
    /// <remarks>
    /// Required format: `Major.Minor.Patch` (e.g. `1.0.0`)
    /// </remarks>
    [JsonPropertyName("version")]
    [JsonRequired]
    public ManifestVersion Version { get; set; }

    /// <summary>
    /// The type of the resource.
    /// </summary>
    [JsonPropertyName("type")]
    [JsonRequired]
    public string Type { get; set; }

    /// <summary>
    /// Only required if the resource is a plugin.
    /// The entry point dll file of the plugin.
    /// </summary>
    [JsonPropertyName("entryPoint")]
    public string? EntryPoint { get; set; }

    /// <summary>
    /// An additional folder with other dll dependencies to add to the library search path.
    /// </summary>
    [JsonPropertyName("libraryFolder")]
    public string? LibraryFolder { get; set; }

    /// <summary>
    /// A list of dependencies that this plugin requires to function.
    /// </summary>
    [JsonPropertyName("relationships")]
    public Relationship[] Relationships { get; set; } = Array.Empty<Relationship>();

    /// <summary>
    /// Whether the manifest is embedded in an assembly.
    /// </summary>
    [JsonPropertyName("embedded")]
    public bool Embedded { get; set; } = false;

    /// <summary>
    /// Any overflow data that is not handled in the base manifest file
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public string ManifestPath { get; set; } = string.Empty;

    public void UpdateFile(Manifest other) {
        Name = other.Name;
        Assets = other.Assets;
        IgnoredEntries = other.IgnoredEntries;
        ClearResidualFiles = other.ClearResidualFiles;
        Version = other.Version;
        Type = other.Type;
        EntryPoint = other.EntryPoint;
        LibraryFolder = other.LibraryFolder;
        Relationships = other.Relationships;

        if (Embedded || string.IsNullOrEmpty(ManifestPath))
            return;

        if (!File.Exists(ManifestPath)) {
            UpdaterLogger.LogWarn($"Manifest file {ManifestPath} did not exist");
        }

        Save(ManifestPath);
    }

    public void Save(string path) {
        File.WriteAllText(path,
            JsonSerializer.Serialize(this,
                new JsonSerializerOptions() {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                })
            );
    }

    public virtual bool Equals(Manifest? other) {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name &&
               Id == other.Id &&
               Assets == other.Assets &&
               EqualityComparer<string[]?>.Default.Equals(IgnoredEntries, other.IgnoredEntries) &&
               ClearResidualFiles == other.ClearResidualFiles &&
               Version == other.Version &&
               Type == other.Type &&
               Embedded == other.Embedded &&
               EntryPoint == other.EntryPoint &&
               LibraryFolder == other.LibraryFolder &&
               Relationships.SequenceEqual(other.Relationships);
    }
}
