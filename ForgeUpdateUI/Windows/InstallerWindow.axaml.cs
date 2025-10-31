using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using ForgeUpdateUI.Services;

using System;
using System.Threading.Tasks;

namespace ForgeUpdateUI;

public partial class InstallerWindow : Window {
    private readonly InstallationService? installService;
    private readonly StoreService? storeService;

    public InstallerWindow() {
        InitializeComponent();

        this.installService = null;
        this.storeService = null;
    }

    public InstallerWindow(LoggerService loggerService, InstallationService installService, StoreService storeService) {
        InitializeComponent();

        this.installService = installService;
        this.storeService = storeService;

        this.InstallerVersion.Text = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";

        (string path, Exception? pathFindException) = installService.FindS4Path();
        this.InstallationPath.Text = path;

        if (pathFindException != null) {
            this.InstallationPathFailedCause.Text = pathFindException.Message;
            this.StartInstallButton.IsEnabled = false;
        } else {
            this.StartInstallButton.IsEnabled = true;
            this.InstallationPathFailed.IsVisible = false;
            loggerService.LogInfo("Found S4 path: {0}", path);
        }
    }

    public void ValidatePath() {
        if (this.InstallationPath.Text == null || this.InstallationPath.Text.Trim() == "") {
            this.InstallationPathFailedCause.Text = "Path cannot be empty";
            this.InstallationPathFailed.IsVisible = true;
            this.StartInstallButton.IsEnabled = false;
            return;
        }

        bool pathValid = this.installService!.ValidateS4Path(this.InstallationPath.Text ?? "", out string? validationError);

        if (pathValid != true) {
            this.InstallationPathFailedCause.Text = validationError ?? "";
            this.InstallationPathFailed.IsVisible = true;
            this.StartInstallButton.IsEnabled = false;
            return;
        }

        this.StartInstallButton.IsEnabled = true;
        this.InstallationPathFailedCause.Text = validationError ?? "";
        this.InstallationPathFailed.IsVisible = false;
    }

    public void CloseWindow(object? sender, RoutedEventArgs e) {
        Close();
    }
    public void PathChanged(object? sender, TextChangedEventArgs e) {
        ValidatePath();
    }
    public void StartInstall(object? sender, RoutedEventArgs e) {
        ValidatePath();
        if (this.StartInstallButton.IsEnabled != true)
            return;

    }
    public async void ChangePath(object? sender, RoutedEventArgs e) {
        var folder = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions {
            Title = "Select Settlers 4 installation folder",
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(this.InstallationPath.Text ?? "")),
            AllowMultiple = false,
        });

        if (folder.Count == 0)
            return;

        string chosenPath = folder[0].Path.LocalPath;
        this.StartInstallButton.IsEnabled = true;
        this.InstallationPath.Text = chosenPath;
        ValidatePath();
    }

}