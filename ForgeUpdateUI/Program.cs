using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

using ForgeUpdateUI.Services;

using Microsoft.Extensions.DependencyInjection;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace ForgeUpdateUI {
    internal class Program {
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

            if (args.Contains("--check")) {
                StoreService storeService = Services.GetService<StoreService>()!;
                await storeService.ReadStoreState();
                if (storeService.Stores.Any(store => store.UpdateAvailable)) {
                    Environment.Exit(1);
                }

                Environment.Exit(0);
            } else {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
