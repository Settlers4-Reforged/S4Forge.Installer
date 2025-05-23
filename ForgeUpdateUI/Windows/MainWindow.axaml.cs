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

            bool autoClose = Environment.GetCommandLineArgs().Contains("--auto-close");

#if DEBUG
            this.AttachDevTools();
#endif

            loggerService.Logs.Subscribe((log) => {
                Dispatcher.UIThread.Post(() => {
                    LogText.Text += log;
                    LogScroll.ScrollToEnd();
                });
            });

            storeService.Update(UpdateManifests).ContinueWith((_) => {
                if (autoClose) {
                    Dispatcher.UIThread.Post(() => {
                        Close();
                    });
                }
            });
        }

        private void UpdateManifests(List<UpdateItem> values) {
            Dispatcher.UIThread.Post(() => {
                UpdateItems.ItemsSource = values.GroupBy((i) => i.Name)
                    .Select(g => g.Last())
                    .OrderBy(i => i.Name);
            });
        }
    }
}