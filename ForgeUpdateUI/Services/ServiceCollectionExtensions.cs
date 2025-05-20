using ForgeUpdateUI.Windows;

using Microsoft.Extensions.DependencyInjection;

namespace ForgeUpdateUI.Services {
    public static class ServiceCollectionExtensions {
        public static void AddCommonServices(this IServiceCollection collection) {
            collection.AddSingleton<StoreService>();
            collection.AddSingleton<LoggerService>();

            if (!Program.Headless)
                AddWindows(collection);
        }

        private static void AddWindows(IServiceCollection collection) {
            collection.AddTransient<MainWindow>();
        }
    }

}
