using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

using ForgeUpdateUI.Services;

using Microsoft.Extensions.DependencyInjection;

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ForgeUpdateUI {
    internal class Program {
        public static bool Headless => Environment.GetCommandLineArgs().Contains("--headless", StringComparer.OrdinalIgnoreCase);

        static IServiceProvider? _services = null;
        public static IServiceProvider Services => _services ??= ConfigureServices();

        private static IServiceProvider ConfigureServices() {
            ServiceCollection services = new ServiceCollection();
            services.AddCommonServices();
            return services.BuildServiceProvider();
        }

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static async Task Main(string[] args) {
            try {
                if (args.Contains("--check")) {
                    StoreService storeService = Services.GetService<StoreService>()!;
                    await storeService.ReadStoreState();
                    if (storeService.Stores.Any(store => store.UpdateAvailable)) {
                        Environment.Exit(1);
                    }

                    Environment.Exit(0);
                } else if (Headless) {
                    AttachConsole(-1);

                    StoreService storeService = Services.GetService<StoreService>()!;
                    LoggerService loggerService = Services.GetService<LoggerService>()!;

                    int previousLogPosition = 0;
                    loggerService.Logs.Subscribe((log) => {
                        var newLog = log.AsSpan(previousLogPosition);
                        previousLogPosition += newLog.Length;
                        Console.WriteLine(newLog.ToString().Trim());
                    });

                    await storeService.Update();
                } else {
                    StartApp(args);
                }

            } catch (Exception e) {
                Console.WriteLine(e);
                Environment.Exit(1);
            }
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
