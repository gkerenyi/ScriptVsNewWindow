using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ScriptVsNewWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> log = new();
        private readonly CoreWebView2CreationProperties webView2CreationProperties;

        private bool enableLog = true;

        private int newWindowTop;
        private int newWindowLeft;

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();

            this.Top = 0;
            this.Left = 1100;

            var executablePath = GetCanaryWebViewPathIfAvailable();
            this.webView2CreationProperties = new CoreWebView2CreationProperties()
            {
                AdditionalBrowserArguments = "--auto-open-devtools-for-tabs",
            };
            if (executablePath != null)
            {
                this.webView2CreationProperties.BrowserExecutableFolder = executablePath;
            }

            _ = InitializeAsync();
        }

        private string? GetCanaryWebViewPathIfAvailable() =>
            Directory
                .EnumerateDirectories(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge SxS\Application"), "*", SearchOption.TopDirectoryOnly)
                .Where(path => Version.TryParse(Path.GetFileName(path), out _))
                .FirstOrDefault();

        public ObservableCollection<string> Log => this.log;

        private async Task InitializeAsync()
        {
            this.WebView.CreationProperties = this.webView2CreationProperties;
            await this.WebView.EnsureCoreWebView2Async();
            await this.WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("console.log('script injected')");

            this.WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            this.WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            this.WebView.NavigateToString(HTML.OpenWindow);
            LogEvent($"Using WebView2 version {this.WebView.CoreWebView2.Environment.BrowserVersionString}");
        }


        private void LogEvent(string @event)
        {
            if (this.enableLog)
            {
                this.log.Add($"[{DateTime.Now.ToString("hh:mm:ss.ffff")}] {@event}");
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Log)));
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (sender is CoreWebView2 webView)
            {
                LogEvent($"NavigationStarting - '{webView.Source}'");
            }
        }

        private void CoreWebView2_ContentLoading(object? sender, CoreWebView2ContentLoadingEventArgs e)
        {
            if (sender is CoreWebView2 webView)
            {
                LogEvent($"ContentLoading - '{webView.Source}'");
            }
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (sender is CoreWebView2 webView)
            {
                LogEvent($"NavigationCompleted - '{webView.Source}'");
            }
        }

        private async void CoreWebView2_NewWindowRequested(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs e)
        {
            if (e.Uri == "https://random.test.url/")
            {
                e.Handled = true;
                _ = Task.Factory.StartNew(() => StartPostNavigationTest(), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
                return;
            }

            var _deferral = e.GetDeferral();

            if (this.ScheduleNewWindow.IsChecked == true)
            {
                _ = Dispatcher.InvokeAsync(() => OpenNewWindowAsync(e, _deferral));
            }
            else
            {
                await OpenNewWindowAsync(e, _deferral);
            }
        }

        private void StartPostNavigationTest()
        {
            this.WebView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
            this.WebView.CoreWebView2.Navigate("https://lite.duckduckgo.com/lite");

            void CoreWebView2_DOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
            {
                this.WebView.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;

                this.WebView.CoreWebView2.ExecuteScriptAsync("""
                    if (window.location.href === 'https://lite.duckduckgo.com/lite') {
                        const form = document.getElementsByTagName('form')[0]
                        form.setAttribute('target', '_blank')
                        document.querySelectorAll('input[class="query"]')[0].value = 'test'
                        form.submit()
                    }
                """);
            }
        }

        private async Task OpenNewWindowAsync(CoreWebView2NewWindowRequestedEventArgs e, CoreWebView2Deferral deferral)
        {
            this.newWindowLeft += 20;
            this.newWindowTop += 20;

            Window window = new Window
            {
                Width = Width,
                Height = Height,
                Left = this.newWindowLeft,
                Top = this.newWindowTop
            };

            var newWebView = new WebView2();
            newWebView.CreationProperties = this.webView2CreationProperties;

            window.Owner = this;
            window.Content = newWebView;
            window.Show();

            await newWebView.EnsureCoreWebView2Async();
            LogEvent($"Creating new WebView2 version {newWebView.CoreWebView2.Environment.BrowserVersionString}");

            newWebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            newWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            newWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            newWebView.CoreWebView2.ContentLoading += CoreWebView2_ContentLoading;
            
            if (this.SetScripts.SelectedIndex == 1)
            {
                LogEvent($"Start Loading Scripts");
                await newWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("console.log('script injected - before setting NewWindow')");
                if (this.Delay.SelectedIndex > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1000 * this.Delay.SelectedIndex));
                }
                LogEvent($"Completed Loading Scripts");
            }


            if (this.SetNewWindow.IsChecked == true)
            {
                LogEvent($"Assigning NewWindow");
                e.NewWindow = newWebView.CoreWebView2;
                LogEvent($"Assigned NewWindow");
            }


            if (this.SetScripts.SelectedIndex == 0)
            {
                LogEvent($"Start Loading Scripts");
                await newWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("console.log('script injected - after setting NewWindow')");
                if (this.Delay.SelectedIndex > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1000 * this.Delay.SelectedIndex));
                }
                LogEvent($"Completed Loading Scripts");
            }

            if (this.SetSource.IsChecked == true)
            {
                LogEvent($"Setting Source - '{e.Uri}'");
                newWebView.Source = new Uri(e.Uri);
                LogEvent($"Set Source - '{e.Uri}'");
            }

            e.Handled = true;
            deferral.Complete();
        }

        private void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            var builder = new StringBuilder();

            foreach (var @event in this.log)
            {
                builder.AppendLine(@event);
            }

            Clipboard.SetText(builder.ToString());
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            this.log.Clear();
        }

        private void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && sender is TextBox textBox &&
                (Uri.TryCreate(textBox.Text, UriKind.Absolute, out var uri) || Uri.TryCreate($"https://{textBox.Text}", UriKind.Absolute, out uri)))
            {
                this.WebView.CoreWebView2.Navigate(uri.AbsoluteUri);
            }
        }

        private void OpenDefaultHomepage(object sender, RoutedEventArgs e)
        {
            this.WebView.NavigateToString(HTML.OpenWindow);
        }
    }
}
