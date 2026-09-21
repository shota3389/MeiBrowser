using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Core;
using Microsoft.Win32;

namespace GUI.Views
{
    public partial class SetupView : UserControl
    {
        public event EventHandler<PackageSelection>? Confirmed;

        private string? selectedGame;
        private string? selectedServer;
        private string? selectedVersion;
        private string? selectedCategory;
        private string? selectedMode;
        private string stokenBuildData = "";

        private string customSophonUrl = "";
        private string? currentPackageId;
        private string? currentPassword;
        private string? preDownloadPassword;

        public SetupView()
        {
            InitializeComponent();

            ModeCombo.ItemsSource = new[]
            {
                new ComboBoxItem() { Content = Localization.T("Mode_Sophon"), Tag = "Sophon" },
                new ComboBoxItem() { Content = Localization.T("Mode_ScatteredFiles"), Tag = "Scattered Files" }
            };
        }

        /// <summary>
        /// The mode a combo box entry stands for. The caption is translated, so the tag
        /// carries the value the rest of the code compares against.
        /// </summary>
        private static string? ModeOf(object? item) =>
            item is ComboBoxItem { Tag: string tag } ? tag
            : item is ComboBoxItem plain ? plain.Content?.ToString()
            : null;

        private void ShowLoading(bool busy)
        {
            LoadingOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (busy) TaskbarProgress.Indeterminate();
            else TaskbarProgress.Clear();
        }

        private void ResetGameCombo()
        {
            GameCombo.ItemsSource = null;

            // Id is what the rest of the code matches on; Name is only ever displayed
            var source = new System.Collections.ArrayList
            {
                new GameEntry("hk4e", Localization.T("Game_Genshin"), "pack://application:,,,/icons/hk4e.png", "Game_Genshin"),
                new GameEntry("hkrpg", Localization.T("Game_StarRail"), "pack://application:,,,/icons/hkrpg.png", "Game_StarRail"),
                new GameEntry("nap", Localization.T("Game_ZZZ"), "pack://application:,,,/icons/nap.png", "Game_ZZZ"),
                // TODO: add hi3 support
            };

            if (selectedMode == "Sophon")
                source.Add(new GameEntry("custom", Localization.T("Game_CustomSophon"),
                    "pack://application:,,,/icons/custom.png", "Game_CustomSophon"));

            GameCombo.ItemsSource = source;
        }

        /// <summary>One row of the game picker. <see cref="Id"/> never changes with the language.</summary>
        public sealed class GameEntry
        {
            public GameEntry(string id, string name, string icon, string key)
            {
                Id = id;
                Name = name;
                Icon = icon;
                Key = key;
            }

            /// <summary>Stable identifier used by the download logic.</summary>
            public string Id { get; }

            /// <summary>Translated caption.</summary>
            public string Name { get; }

            public string Icon { get; }

            /// <summary>Translation key this row was built from.</summary>
            public string Key { get; }
        }

        #region mode selection
        private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedMode = ModeOf(ModeCombo.SelectedItem);

            ResetGameCombo();
            GameCombo.IsEnabled = true;

            ServerCombo.IsEnabled = false;
            ServerCombo.ItemsSource = null;

            VersionCombo.IsEnabled = false;
            VersionCombo.ItemsSource = null;

            CategoryCombo.IsEnabled = false;
            CategoryCombo.ItemsSource = null;

            DiffMode.IsChecked = false;
            DiffMode.IsEnabled = false;

            ConfirmButton.IsEnabled = false;
        }

        private void ModeHelpButton_Click(object sender, RoutedEventArgs e)
        {
            ThemedDialog.ShowLocalized(
                "Setup_ModeInfo_Body",
                "Setup_ModeInfo_Title", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region game selection
        private async void GameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameCombo.SelectedItem == null) return;
            // selectedGame keeps the stable id, not the translated caption
            selectedGame = (GameCombo.SelectedItem as GameEntry)?.Id;

            ServerCombo.ItemsSource = null;
            VersionCombo.ItemsSource = null;

            CustomSophonTitle.Visibility = Visibility.Hidden;
            CustomSophonUrl.Visibility = Visibility.Hidden;
            CustomSophonUrl.Text = "";
            customSophonUrl = "";
            CheckSophonButton.Visibility = Visibility.Hidden;

            ServerTitle.Visibility = Visibility.Visible;
            ServerCombo.Visibility = Visibility.Visible;

            if (selectedMode == "Sophon")
            {
                if (selectedGame == "custom")
                {
                    CustomSophonTitle.Visibility = Visibility.Visible;
                    CustomSophonUrl.Visibility = Visibility.Visible;
                    CheckSophonButton.Visibility = Visibility.Visible;

                    ServerTitle.Visibility = Visibility.Hidden;
                    ServerCombo.Visibility = Visibility.Hidden;
                }
                else
                {
                    ServerCombo.ItemsSource = new[]
                    {
                        new ComboBoxItem() { Content = Localization.T("Region_OS"), Tag = "OS" },
                        new ComboBoxItem() { Content = Localization.T("Region_CN"), Tag = "CN" }
                    };

                    VersionCombo.IsEnabled = false;
                }
                ServerCombo.IsEnabled = true;
            }
            else
            {
                ShowLoading(true);
                try
                {
                    var versions = await Dispatch.GetDispatchVersions(selectedGame!);
                    VersionCombo.ItemsSource = versions;
                    VersionCombo.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    Report("Setup_ErrVersionsForGame", ex);
                }
                finally
                {
                    ShowLoading(false);
                }
            }

            CategoryCombo.IsEnabled = false;
            CategoryCombo.ItemsSource = null;

            DiffMode.IsChecked = false;
            DiffMode.IsEnabled = false;

            ConfirmButton.IsEnabled = false;
        }

        private async void CheckSophonButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLoading(true);

            try
            {
                customSophonUrl = CustomSophonUrl.Text;
                var version = await Sophon.CheckBuild(customSophonUrl);
                VersionCombo.ItemsSource = null;
                VersionCombo.ItemsSource = new[] { version };
                VersionCombo.IsEnabled = true;

                CategoryCombo.ItemsSource = null;
                CategoryCombo.IsEnabled = false;

                ConfirmButton.IsEnabled = false;
            }
            catch
            {
                ThemedDialog.ShowLocalized("Setup_ErrSophonBuild", "Common_Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }
        #endregion

        #region server selection
        private async void ServerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerCombo.SelectedItem == null) return;
            selectedServer = (ServerCombo.SelectedItem as ComboBoxItem)?.Tag as string
                             ?? (ServerCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();

            VersionCombo.IsEnabled = false;
            CategoryCombo.IsEnabled = false;
            VersionCombo.ItemsSource = null;
            CategoryCombo.ItemsSource = null;
            DiffMode.IsChecked = false;
            DiffMode.IsEnabled = false;
            ConfirmButton.IsEnabled = false;

            currentPackageId = null;
            currentPassword = null;
            preDownloadPassword = null;

            ShowLoading(true);
            try
            {
                dynamic metaData = await Meta.GetVersions(selectedGame!, selectedServer!);
                var versions = (List<string>)metaData.Item1;
                currentPackageId = (string)metaData.Item2;
                currentPassword = (string)metaData.Item3;
                if ((string)metaData.Item4 != "")
                    preDownloadPassword = (string)metaData.Item4;

                VersionCombo.ItemsSource = versions;
                VersionCombo.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Report("Setup_ErrVersionsForServer", ex);
            }
            finally
            {
                ShowLoading(false);
            }
        }
        #endregion

        #region version selection
        private async void VersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionCombo.SelectedItem == null) return;
            selectedVersion = VersionCombo.SelectedItem?.ToString();

            CategoryCombo.IsEnabled = false;
            CategoryCombo.ItemsSource = null;
            DiffMode.IsChecked = false;
            DiffMode.IsEnabled = false;
            ConfirmButton.IsEnabled = false;

            ComboBoxItem[] packageItems;

            ShowLoading(true);
            try
            {
                if (selectedMode == "Sophon")
                {
                    var password = currentPassword;
                    if (preDownloadPassword != null && selectedVersion!.EndsWith(" (pre-download)"))
                        password = preDownloadPassword;

                    // The list is already ordered newest first locally, no need to sort on the server
                    var packages = selectedGame == "custom"
                        ? await Meta.GetCustomPackages(customSophonUrl)
                        : await Meta.GetPackages(selectedServer!, selectedVersion, currentPackageId!, password!);

                    packageItems = packages.Select(p =>
                        new ComboBoxItem() { Content = $"{p[1]} - {p[2]}", Tag = p[0] }
                    ).ToArray();
                }
                else
                {
                    List<string> packages = await Dispatch.GetPackages(selectedGame!, selectedVersion!);

                    packageItems = packages.Select(p =>
                        new ComboBoxItem() { Content = p, Tag = p.ToLower() }
                    ).ToArray();
                }

                CategoryCombo.ItemsSource = packageItems;
                CategoryCombo.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Report("Setup_ErrPackagesForVersion", ex);
            }
            finally
            {
                ShowLoading(false);
            }
        }
        #endregion

        #region package selection
        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedCategory = (CategoryCombo.SelectedItem as ComboBoxItem)?.Tag as string;
            DiffMode.IsChecked = false;
            DiffMode.IsEnabled = false;

            // Emptying the list raises this event as well, need to handle it
            if (selectedCategory == null)
            {
                ConfirmButton.IsEnabled = false;
                return;
            }

            DiffMode.IsEnabled = selectedMode == "Sophon" && HasOlderVersion();
            ConfirmButton.IsEnabled = true;
        }

        private bool HasOlderVersion()
        {
            int index = VersionCombo.SelectedIndex;
            return index >= 0 && index + 1 < VersionCombo.Items.Count;
        }
        #endregion

        private void Confirm_Click(object? sender = null, RoutedEventArgs? e = null)
        {
            string version = selectedMode == "Sophon" ? $"{selectedVersion}.0" : selectedVersion ?? "";
            string region = selectedGame == "custom" ? customSophonUrl : selectedServer ?? "";

            string? previousVersion = null;
            if (DiffMode.IsChecked == true && HasOlderVersion())
                previousVersion = VersionCombo.Items[VersionCombo.SelectedIndex + 1]?.ToString();

            Confirmed?.Invoke(this, new PackageSelection(
                selectedGame ?? "",
                region,
                version,
                selectedCategory ?? "",
                selectedMode ?? "Sophon",
                previousVersion,
                stokenBuildData));

            stokenBuildData = "";
        }

        private void STokenButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "SToken Build|getBuildWithStokenLogin.json|JSON Files (*.json)|*.json",
                Multiselect = false
            };

            if (dlg.ShowDialog() == true)
            {
                string json = File.ReadAllText(dlg.FileName);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    stokenBuildData = json;
                    selectedMode = "Sophon";
                    Confirm_Click();
                }
            }
        }

        private static void Report(string messageKey, Exception ex)
        {
            string message = Localization.T(messageKey);
            Console.WriteLine($"{message}\n{ex}");
            ThemedDialog.Show($"{message}\n\n{ex.Message}", Localization.T("Common_Error"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
