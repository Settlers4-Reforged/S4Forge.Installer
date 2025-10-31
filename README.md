# S4Forge.Installer

This repository contains both the installer/updater application for S4Forge modules as well as the library that can be used to integrate the installation and updating process into other applications.

ForgeUpdater is the shared library providing the core functionality. It's shared via NuGet as `S4Forge.Updater` package.

ForgeUpdaterUI takes that library and wraps it into an application that both serves as installer and updater for Settlers4-Reforged. You can download the latest updater version from the releases section. It's pre-configured with the Settlers4-Reforged installation config.

## Installation and updating process

There are two parts to how ForgeUpdater works:

- The modules manifest file which contains information about a modules dependencies, its version, and where to download that versions assets from.
- The installation config file which contains the installation paths and a list of what modules are (to be) installed and where to get their manifest from

> The word "download" is mentioned here, but ForgeUpdater also works with local files, so you can also use it to update modules that are not hosted on the internet.

You can think of a installation config file as a list of modules that you want to install.

ForgeUpdaterUI works in two different modes: installation mode and update mode.
- In installation mode, it reads the installation config file, downloads the manifests for each module, and installs them into the specified paths.
- In update mode, it checks the local installation against the installation config file, downloads any new manifests + modules, and updates the modules as necessary.

The installation mode is the default startup mode. If ForgeUpdaterUI is started with the `--update` command line argument, it will start in update mode.

### Manifest file

The manifest file is a JSON file that describes a module, its dependencies, and other metadata.
You don't have to create this file manually, the SDK provides MSBuild targets to generate it for you. Just add a `<PackageReference Include="S4Forge.SDK" Version="*" PrivateAssets="all" />` to your project file and the SDK will automatically generate a `manifest.json` file in the output directory when you build your project (When you have the `Embedded` flag not set/set to false).
See the [S4Forge.SDK documentation](https://github.com/Settlers4-Reforged/S4Forge.SDK/blob/main/ForgeSDK/README.md) for further information about the MSBuild targets.

That manifest file needs to be placed in the root of your module project, so that ForgeUpdater can find it. The file should be named `manifest.json`.

> A manifest can also be embedded in the module assembly, but this is not recommended for most use cases.

An example `manifest.json` looks as follows:

```json
{
  "name": "UX-Engine SDL Renderer",
  "id": "UX-Engine-SDL",
  "assets": {
    "uri": "https://github.com/Settlers4-Reforged/S4Forge.SDLModule/releases/latest/download/S4Forge-SDLBackend.0.2.0.zip"
  },
  "ignoredEntries": ["textures/themes/"],
  "clearResidualFiles": false,
  "version": "0.2.0",
  "type": "module",
  "entryPoint": "S4Forge-SDLBackend.dll",
  "libraryFolder": "Lib/",
  "relationships": [
    {
      "id": "Forge",
      "optional": false,
      "compatibility": {
        "minimum": "1.*",
        "verified": "1.1.0",
        "maximum": "2.0.0"
      }
    },
    {
      "id": "UXEngine",
      "optional": false,
      "compatibility": {
        "minimum": "1.*",
        "verified": "1.1.0",
        "maximum": "2.0.0"
      }
    }
  ],
  "embedded": false
}
```

> For an up to date explanation on what each field actually does and what other, not here shown fields do, check out the [Manifest class description](https://github.com/Settlers4-Reforged/S4Forge.SDK/blob/main/Manifests/Manifest.cs)

### Installation config file

The installation config file is a JSON file that describes the installation paths and the modules to be installed. It contains a list of modules, each with its own manifest URL or local path.
This file is used to describe a "desired" state of a Forge installation.
You can check the current installation config of Forge at: _(WIP)_

An example `installation.json` file looks as follows:

```json
[
  {
    "name": "Modules",
    "keep_residual_files": true,
    "install_into_folders": true,
    "manifest_feeds": [
      {
        "manifest_uri": "https://github.com/Settlers4-Reforged/S4Forge.SDLModule/releases/latest/download/manifest.json"
      },
      {
        "manifest_uri": "https://github.com/Settlers4-Reforged/S4Forge.DebugModule/releases/latest/download/manifest.json"
      }
    ],
    "installation_path": "./Modules/"
  }
]
```
> For an up to date explanation on what each field actually does and what other, not here shown fields do, check out the [Installation class description](https://github.com/Settlers4-Reforged/S4Forge.SDK/blob/main/Installation.cs)

This config will download the manifests from the specified URLs and install the modules into the `./Modules/` directory (This relative path is from the perspective of the json file, regardless where the updater was started from. It can also be an absolute path, but that's not recommended for final installations/distribution). The `keep_residual_files` option indicates whether to keep files that are not part of the module but were previously installed, and `install_into_folders` indicates whether to create a subfolder for each module.
