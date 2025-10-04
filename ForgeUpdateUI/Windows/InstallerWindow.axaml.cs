using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using ForgeUpdateUI.Services;

using System;

namespace ForgeUpdateUI;

public partial class InstallerWindow : Window {
    public InstallerWindow() {
        InitializeComponent();
    }

    public InstallerWindow(LoggerService loggerService, InstallationService installService) {
        InitializeComponent();

        this.InstallerVersion.Text = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";

        (string path, Exception? pathFindException) = installService.FindS4Path();
        this.InstallationPath.Text = path;

        if (pathFindException != null) {
            this.InstallationPathFailedCause.Text = pathFindException.Message;
        } else {
            this.InstallationPathFailed.IsVisible = false;
            loggerService.LogInfo("Found S4 path: {0}", path);
        }
    }

    public void CloseWindow(object? sender, RoutedEventArgs e) {
        Close();
    }
    public void StartInstall(object? sender, RoutedEventArgs e) {
        throw new NotImplementedException("Installation logic is not implemented yet.");
    }
}