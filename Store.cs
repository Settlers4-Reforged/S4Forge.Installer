using ForgeUpdater.Manifests;
using ForgeUpdater.Updater;

using System.Text.Json.Serialization;

namespace ForgeUpdater;

public record Store {
    public Store(string installationPath, params string[] remoteStorePaths) {
        InstallationPath = installationPath;
        RemoteStorePaths = remoteStorePaths;
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("installation_path")]
    public string InstallationPath { get; set; }

    [JsonPropertyName("remote_store_paths")]
    public string[]? RemoteStorePaths { get; set; }


    [JsonPropertyName("install_into_folders")]
    public bool InstallIntoFolders { get; set; } = true;

    [JsonPropertyName("keep_residual_files")]
    public bool KeepResidualFiles { get; set; } = UpdaterConfig.DefaultUpdateShouldClearResidualFiles;
}
