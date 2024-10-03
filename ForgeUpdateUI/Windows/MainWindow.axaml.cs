using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

using ForgeUpdateUI.Models;
using ForgeUpdateUI.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ForgeUpdateUI.Windows {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        public void CloseWindow(object? sender, RoutedEventArgs e) {
            Close();
        }

        public MainWindow(StoreService storeService, LoggerService loggerService) {
            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif

            loggerService.Logs.Subscribe((log) => {
                Dispatcher.UIThread.Post(() => {
                    LogText.Text = log;
                    LogScroll.ScrollToEnd();
                });
            });

            List<UpdateItem> values = new List<UpdateItem>();
            storeService.ReadStoreState().ContinueWith(async (_) => {
                values.AddRange(storeService.Stores.SelectMany(store => store.ManifestsToUpdate).Select(pair => pair.source).Where(s => s != null).Select(manifest => new UpdateItem() {
                    Name = manifest!.Name,
                    Progress = "",
                    Version = manifest.Version.ToString()
                }));


                var list = storeService.UpdateAll();

                await foreach (var update in list) {
                    values.Add(update);
                    Dispatcher.UIThread.Post(() => {
                        UpdateItems.ItemsSource = values.GroupBy((i) => i.Name)
                            .Select(g => g.Last())
                            .OrderBy(i => i.Name);
                    });
                }
            });
        }
    }
}