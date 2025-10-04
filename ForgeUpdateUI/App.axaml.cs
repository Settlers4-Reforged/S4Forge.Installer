using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using ForgeUpdateUI.Windows;

using Microsoft.Extensions.DependencyInjection;

namespace ForgeUpdateUI {
    public partial class App : Application {
        public override void Initialize() {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted() {
            Window targetWindow;
            if (Program.IsInstaller) {
                targetWindow = Program.Services.GetRequiredService<InstallerWindow>();
            } else {
                targetWindow = Program.Services.GetRequiredService<MainWindow>();
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                desktop.MainWindow = targetWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}