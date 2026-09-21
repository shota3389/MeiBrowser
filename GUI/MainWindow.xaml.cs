using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using AvalonDock.Layout;
using Core;
using Dark.Net;
using GUI.Views;
using Microsoft.Win32;

namespace GUI
{
    public partial class MainWindow : Window, IRelocalizable
    {
        private readonly string appVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).FileVersion;
        private bool isInitializing = true;

        private SetupView setupView = null!;
        private LayoutDocument? downloadDocument;
        private DownloadView? downloadView;

        /// Loaded tabs that should come back after the window is rebuilt for a new language
        private readonly System.Collections.Generic.List<RestoredTab> restoredTabs = new();

        /// Set when the user chose to quit while a download was still unwinding
        private bool quitAfterDownload;

        private ResourceDictionary? currentThemeDictionary;

        /// A package tab that was open when the window had to be rebuilt
        private sealed record RestoredTab(PackageSelection Selection);

        public MainWindow()
        {
            InitializeComponent();
            DarkNet.Instance.SetWindowThemeWpf(this, Theme.Dark);

            Console.WriteLine($"MeiBrowser v{appVersion} starting, Hello World !");

            this.Title = $"MeiBrowser v{appVersion} - @Escartem <3";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var saved = AppThemes.Find(AppSettings.SelectedTheme);
            ApplyTheme(saved);

            ThemeSelector.ItemsSource = AppThemes.All;
            ThemeSelector.SelectedItem = saved;

            LanguageSelector.ItemsSource = new[]
            {
                new LanguageOption(AppLanguage.Chinese),
                new LanguageOption(AppLanguage.English)
            };
            LanguageSelector.SelectedItem = ((LanguageOption[])LanguageSelector.ItemsSource)
                .FirstOrDefault(o => o.Language == Localization.Current);

            isInitializing = false;
            BuildFixedTabs();

            // Tabs that were open before a language switch come back once the shell is ready
            foreach (var tab in restoredTabs.ToList())
                _ = Setup_ConfirmedAsync(tab.Selection);
            restoredTabs.Clear();
        }

        #region tabs

        private void BuildFixedTabs()
        {
            setupView = new SetupView();
            setupView.Confirmed += Setup_Confirmed;

            DocumentPane.Children.Add(new LayoutDocument
            {
                Title = Localization.T("Tab_Setup"),
                Content = setupView,
                CanClose = false,
                CanFloat = true
            });

            BottomPane.Children.Add(new LayoutAnchorable
            {
                Title = Localization.T("Tab_Console"),
                Content = new ConsoleView(),
                CanClose = false,
                CanHide = false,
                CanAutoHide = true,
                CanFloat = true
            });
        }

        private void Setup_Confirmed(object? sender, PackageSelection selection) =>
            _ = Setup_ConfirmedAsync(selection);

        private async System.Threading.Tasks.Task Setup_ConfirmedAsync(PackageSelection selection)
        {
            var view = new FileTreeView(selection);

            var document = new LayoutDocument
            {
                Title = selection.Title,
                ToolTip = $"{selection.Mode} - {selection.Region} - {selection.Version}",
                Content = view,
                CanClose = true
            };

            view.DownloadRequested += FileTree_DownloadRequested;
            view.LoadFailed += (_, _) => CloseDocument(document);

            DocumentPane.Children.Add(document);
            document.IsActive = true;

            await view.LoadAsync();
        }

        private static void CloseDocument(LayoutDocument document)
        {
            document.CanClose = true;
            document.Close();
        }
        #endregion

        #region downloads
        private async void FileTree_DownloadRequested(object? sender, DownloadRequest request)
        {
            // TODO: implement multi download
            if (downloadView is { IsRunning: true })
            {
                ThemedDialog.ShowLocalized(
                    "Download_Busy_Body", "Download_Busy_Title",
                    MessageBoxButton.OK, MessageBoxImage.Information, this);

                if (downloadDocument != null)
                    downloadDocument.IsActive = true;
                return;
            }

            var confirm = ThemedDialog.ShowLocalized(
                "Download_Confirm_Body", "Download_Confirm_Title",
                MessageBoxButton.YesNo, MessageBoxImage.Question, this,
                request.Assets.Count, Utils.FormatSize(request.TotalSize));
            if (confirm != MessageBoxResult.Yes)
                return;

            var folder = new System.Windows.Forms.FolderBrowserDialog();
            if (folder.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            // A finished run may still be sitting there
            if (downloadDocument != null)
                CloseDocument(downloadDocument);

            downloadView = new DownloadView();
            downloadDocument = new LayoutDocument
            {
                Title = Localization.T("Tab_Download"),
                Content = downloadView,
                CanClose = false
            };

            var document = downloadDocument;
            downloadView.Finished += (_, outcome) =>
            {
                if (quitAfterDownload)
                {
                    quitAfterDownload = false;
                    Close();
                    return;
                }

                if (outcome == DownloadOutcome.Cancelled || outcome == DownloadOutcome.Dismissed)
                {
                    CloseDocument(document);
                    if (ReferenceEquals(downloadDocument, document))
                    {
                        downloadDocument = null;
                        downloadView = null;
                    }
                    return;
                }

                document.CanClose = true;
                document.Title = Localization.T(
                    outcome == DownloadOutcome.Completed ? "Tab_DownloadDone" : "Tab_DownloadErrors");
            };

            DocumentPane.Children.Add(downloadDocument);
            downloadDocument.IsActive = true;

            await downloadView.RunAsync(request, folder.SelectedPath);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (downloadView is not { IsRunning: true })
                return;

            var answer = ThemedDialog.ShowLocalized("Download_Quit_Body", "Download_Quit_Title",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, this);

            e.Cancel = true;

            if (answer != MessageBoxResult.Yes)
                return;

            if (downloadView.RequestCancel(confirm: false))
                quitAfterDownload = true;
        }
        #endregion

        #region language
        private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            if (LanguageSelector.SelectedItem is not LanguageOption chosen) return;

            var language = chosen.Language;
            if (language == Localization.Current) return;

            // A running download holds a CancellationTokenSource and a progress pipeline that
            // cannot survive the download tab being rebuilt, so the change waits for it
            if (downloadView is { IsRunning: true })
            {
                SelectCurrentLanguage();
                ThemedDialog.ShowLocalized("Language_Busy_Body", "Language_Busy_Title",
                    MessageBoxButton.OK, MessageBoxImage.Information, this);
                return;
            }

            AppSettings.SelectedLanguage = language;
            AppSettings.Save();

            // Raises every caption in the app to the new language
            Localization.Apply(this, language);
        }

        private void SelectCurrentLanguage()
        {
            if (LanguageSelector.ItemsSource is not LanguageOption[] options) return;

            isInitializing = true;
            LanguageSelector.SelectedItem = options.FirstOrDefault(o => o.Language == Localization.Current);
            isInitializing = false;
        }

        /// <summary>
        /// Rebuilds the window in place for the new language: every caption in the XAML of
        /// the window itself is captured at load, so the shell is recreated and the tabs
        /// that were open are put back.
        /// </summary>
        public void Relocalize()
        {
            CaptureOpenTabs();

            var replacement = new MainWindow();
            replacement.restoredTabs.AddRange(restoredTabs);
            CarryOverLayoutTo(replacement);

            // Take this window out of the way before the download tab is released
            Hide();
            ReleaseDownload();

            replacement.Show();
            replacement.Activate();

            // Closing from inside the combo box's SelectionChanged event would tear the window
            // down while that event is still on the stack, so let the current input finish first
            Dispatcher.BeginInvoke(new Action(Close), DispatcherPriority.Background);
        }

        /// <summary>Restores the state of the current window once the replacement is on screen.</summary>
        private void CarryOverLayoutTo(MainWindow replacement)
        {
            replacement.WindowState = WindowState;

            // Only a restored window has usable Left/Top values to copy
            if (WindowState == WindowState.Normal)
            {
                replacement.WindowStartupLocation = WindowStartupLocation.Manual;
                replacement.Width = Width;
                replacement.Height = Height;
                replacement.Left = Left;
                replacement.Top = Top;
            }
        }

        private void CaptureOpenTabs()
        {
            restoredTabs.Clear();

            foreach (var document in DocumentPane.Children.OfType<LayoutDocument>())
            {
                if (document.Content is FileTreeView tree)
                    restoredTabs.Add(new RestoredTab(tree.Selection));
            }
        }

        private void ReleaseDownload()
        {
            if (downloadDocument != null)
            {
                downloadDocument.CanClose = true;
                downloadDocument.Close();
            }

            downloadDocument = null;
            downloadView = null;
        }
        #endregion

        #region theming
        private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            if (ThemeSelector.SelectedItem is not ThemeOption option) return;

            ApplyTheme(option);
            AppSettings.SelectedTheme = option.Name;
            AppSettings.Save();
        }

        private void ApplyTheme(ThemeOption option)
        {
            try
            {
                SwapThemeDictionary(option.Load());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not apply theme '{option.Name}': {ex.Message}");
            }
        }

        private void SwapThemeDictionary(ResourceDictionary theme)
        {
            var merged = Application.Current.Resources.MergedDictionaries;

            if (currentThemeDictionary != null)
                merged.Remove(currentThemeDictionary);
            else if (merged.Count > 0)
                merged.RemoveAt(0); // the one App.xaml merged at startup

            merged.Add(theme);
            currentThemeDictionary = theme;

            var chrome = Theme.Dark;
            if (theme["WindowBackgroundColor"] is Color background)
            {
                double luminance = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255;
                chrome = luminance > 0.5 ? Theme.Light : Theme.Dark;
            }

            DarkNet.Instance.SetWindowThemeWpf(this, chrome);
            DockTheme.Apply(Dock);
        }
        #endregion
    }
}
