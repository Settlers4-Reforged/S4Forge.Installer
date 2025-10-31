using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

using ForgeUpdateUI.Services;

using Microsoft.Extensions.DependencyInjection;

using Sentry;

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ForgeUpdateUI {
    public class Program {
        public static string[] CommandLineArgs { get; private set; } = [];
        public static bool Headless { get; set; }

        public static bool IsInstaller = false;

        static IServiceProvider? _services = null;
        public static IServiceProvider Services => _services ??= ConfigureServices();

        private static IServiceProvider ConfigureServices() {
            ServiceCollection services = new ServiceCollection();
            services.AddCommonServices();
            return services.BuildServiceProvider();
        }

        private static void InitSentry() {
#if !DEBUG
            SentrySdk.Init(options => {
                options.Dsn = "https://4de1b5b092f6f6d2cb26487f3a10c32c@o4509373324918784.ingest.de.sentry.io/4509378699919440";
                options.AutoSessionTracking = true;
                options.Release = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
                options.StackTraceMode = StackTraceMode.Enhanced;
                options.Experimental.EnableLogs = true;
            });
#endif    
        }

        [STAThread]
        public static async Task<int> Main(string[] args) {
            _services = null; // Reset services to ensure a fresh state on each run (useful for tests)

            CommandLineArgs = args;
            Headless = args.Contains("--headless") || args.Contains("-h");
            // Only show installer UI if manually started.
            // This flag should be set by all applications (SU, etc.) when they just want to show an updater UI.
            // Also starting in headless mode should ensure that installer mode is off.
            IsInstaller = !args.Contains("--updater") && !Headless;

            InitSentry();
            LoggerService? loggerService = null;

            try {
                if (args.Contains("--check")) {
                    loggerService = Services.GetService<LoggerService>()!;
                    loggerService.LogInfo("### Forge Updater started in check mode ###");

                    StoreService storeService = Services.GetService<StoreService>()!;
                    await storeService.ReadStoreState();
                    if (storeService.Stores.Any(store => store.UpdateAvailable)) {
                        return 5; // STATUS CODE 5: Updates available - cant use 1 as that is also the generic error code
                    }
                } else if (Headless) {
                    AttachConsole(-1);
                    loggerService = Services.GetService<LoggerService>()!;
                    loggerService.LogInfo("### Forge Updater started ###");

                    StoreService storeService = Services.GetService<StoreService>()!;
                    await storeService.Update();

                    loggerService.LogInfo("### Update complete ###");
                    SentrySdk.CaptureMessage("Update completed");
                } else {
                    loggerService = Services.GetService<LoggerService>()!;
                    StartApp(args);
                    if (IsInstaller) {
                        SentrySdk.CaptureMessage("Installation completed");
                    } else {
                        SentrySdk.CaptureMessage("Update completed");
                    }
                }

            } catch (Exception e) {
                Console.WriteLine(e);
                loggerService?.LogError(e, "Fatal error");
                return 1;
            }

            return 0;
        }

        private static void StartApp(string[] args) {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();



        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
    }
}
