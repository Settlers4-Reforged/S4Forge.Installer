using ForgeUpdater.Manifests;
using ForgeUpdater.Updater;

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ForgeUpdater;

/// <summary>
/// A store describes a location where manifests of a certain type/source are installed.
/// It contains list of feeds of what manifests are to be expected in that folder.
/// </summary>
public record Installation {
    [JsonPropertyName("name")]
    [JsonRequired]
    public string Name { get; set; }

    [JsonPropertyName("installation_path")]
    [JsonRequired]
    public string InstallationPath { get; set; }

    /// <summary>
    /// A manifest feed describes where to find the latest version of a manifest.
    /// </summary>
    public record ManifestFeed {
        /// <summary>
        /// The uri to the newest manifest.
        /// Can be local or remote.
        /// 
        /// This uri should be stable and not change between versions.
        /// </summary>
        [JsonPropertyName("manifest_uri")]
        [JsonRequired]
        public string ManifestUri { get; set; }
    }

    [JsonPropertyName("manifest_feeds")]
    [JsonRequired]
    public ManifestFeed[]? ManifestFeeds { get; set; }

    /// <summary>
    /// Whether or not the updater should install the manifests into a folder named after the id of the manifest.
    /// </summary>
    [JsonPropertyName("install_into_folders")]
    public bool InstallIntoFolders { get; set; } = true;

    /// <summary>
    /// Whether or not the updater should clear installations in the target folder, that are not tracked in the current manifest feed.
    /// </summary>
    [JsonPropertyName("keep_residual_files")]
    public bool KeepResidualFiles { get; set; } = UpdaterConfig.DefaultUpdateShouldClearResidualFiles;
}
