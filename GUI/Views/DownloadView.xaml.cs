using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Core;

namespace GUI.Views
{
    public partial class DownloadView : UserControl
    {
        public event EventHandler<DownloadOutcome>? Finished;

        public ObservableCollection<ActiveDownloadRow> ActiveDownloads { get; } = new();

        private CancellationTokenSource? cancellation;
        private bool isRunning;

        public DownloadView()
        {
            InitializeComponent();
            DataContext = this;
        }

        public bool IsRunning => isRunning;

        public bool RequestCancel(bool confirm = true)
        {
            if (cancellation is not { IsCancellationRequested: false })
                return false;

            if (confirm)
            {
                var answer = ThemedDialog.ShowLocalized(
                    "Download_ConfirmCancel_Body",
                    "Download_ConfirmCancel_Title", MessageBoxButton.YesNo, MessageBoxImage.Question, Window.GetWindow(this));

                if (answer != MessageBoxResult.Yes)
                    return false;
            }

            // The download may well have finished while the confirmation was sitting open
            if (cancellation is not { IsCancellationRequested: false })
                return false;


            CancelDownloadButton.IsEnabled = false;
            CancelDownloadButton.Content = Localization.T("Download_Cancelling");
            TaskbarProgress.Paused(DownloadBar.Value / 100);
            cancellation.Cancel();
            return true;
        }

        public async Task RunAsync(DownloadRequest request, string savePath)
        {
            isRunning = true;
            cancellation = new CancellationTokenSource();
            TaskbarProgress.DownloadActive = true;

            HeadingText.Text = Localization.T("Download_HeadingFor", request.SourceTitle);
            CancelDownloadButton.IsEnabled = true;
            CancelDownloadButton.Content = Localization.T("Download_Cancel");
            DownloadBar.Value = 0;
            DownloadText.Text = Localization.T("Download_Starting");
            DownloadDetail.Text = "";
            ActiveDownloads.Clear();
            ActiveFilesEmpty.Visibility = Visibility.Visible;
            ResetMeters();
            TaskbarProgress.Set(0);

            var progress = new Progress<DownloadProgress>(p =>
            {
                DownloadBar.Value = p.Fraction * 100;
                TaskbarProgress.Set(p.Fraction);

                string speed = p.BytesPerSecond > 0 ? $"{Utils.FormatSize((long)p.BytesPerSecond)}/s" : "--";
                string eta = p.Eta is { } left && left > TimeSpan.Zero ? FormatEta(left) : "--";

                DownloadText.Text = Localization.T("Download_Progress",
                    (p.Fraction * 100).ToString("F1"), Utils.FormatSize(p.BytesCompleted), Utils.FormatSize(p.TotalBytes), speed, eta);

                DownloadDetail.Text = Localization.T("Download_FileCount", p.FilesCompleted, p.TotalFiles) +
                    (p.FilesFailed > 0 ? Localization.T("Download_FileCountFailed", p.FilesFailed) : "");

                UpdateMeters(p);
                SyncActiveRows(p.ActiveFiles);
            });

            DownloadResult? result = null;
            Exception? fatal = null;
            try
            {
                var downloader = new Download();
                result = await downloader.DownloadFilesAsync(
                    request.Assets, request.DownloadUrl, progress, savePath, cancellation.Token);
            }
            catch (Exception ex)
            {
                fatal = ex;
                Console.WriteLine($"Download aborted: {ex}");
            }
            finally
            {
                cancellation.Dispose();
                cancellation = null;
                isRunning = false;
                TaskbarProgress.DownloadActive = false;
                TaskbarProgress.Clear();

                ActiveDownloads.Clear();
                CancelDownloadButton.IsEnabled = true;
                CancelDownloadButton.Content = Localization.T("Common_Close");
            }

            if (fatal != null)
            {
                HeadingText.Text = Localization.T("Download_Failed");
                ActiveFilesEmpty.Text = fatal.Message;
                ActiveFilesEmpty.Visibility = Visibility.Visible;
                TaskbarProgress.Error(1);
                ThemedDialog.ShowLocalized("Download_FailedBody", "Download_Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error, null, fatal.Message);
                Finished?.Invoke(this, DownloadOutcome.Failed);
                return;
            }

            if (result == null)
            {
                Finished?.Invoke(this, DownloadOutcome.Failed);
                return;
            }

            if (result.Cancelled)
            {
                ThemedDialog.ShowLocalized("Download_CancelledBody", "Common_Cancelled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Finished?.Invoke(this, DownloadOutcome.Cancelled);
                return;
            }

            if (result.AllSucceeded)
            {
                HeadingText.Text = Localization.T("Download_Complete");
                ActiveFilesEmpty.Text = Localization.T("Download_AllVerified", result.Succeeded);
                ActiveFilesEmpty.Visibility = Visibility.Visible;
                DownloadBar.Value = 100;

                ThemedDialog.Show(
                    Localization.T(result.Skipped > 0 ? "Download_AllVerifiedWithSkipped" : "Download_AllVerified",
                                   result.Succeeded, result.Skipped),
                    Localization.T("Common_Done"), MessageBoxButton.OK, MessageBoxImage.Information);
                Finished?.Invoke(this, DownloadOutcome.Completed);
                return;
            }

            HeadingText.Text = Localization.T("Download_WithErrors");
            ActiveFilesEmpty.Text = Localization.T("Download_WithErrorsSummary", result.Succeeded, result.Failures.Count);
            ActiveFilesEmpty.Visibility = Visibility.Visible;
            TaskbarProgress.Error(1);

            var sample = string.Join("\n", result.Failures.Take(10).Select(f => $"- {Shorten(f.AssetName)}: {f.Reason}"));
            if (result.Failures.Count > 10)
                sample += Localization.T("Download_WithErrorsMore", result.Failures.Count - 10);

            ThemedDialog.Show(
                Localization.T("Download_WithErrorsBody", result.Succeeded, result.Failures.Count, sample),
                Localization.T("Download_WithErrors"), MessageBoxButton.OK, MessageBoxImage.Warning);
            Finished?.Invoke(this, DownloadOutcome.CompletedWithErrors);
        }

        private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                RequestCancel();
                return;
            }

            // The run is over and the button now says Close
            Finished?.Invoke(this, DownloadOutcome.Dismissed);
        }

        #region meters
        private void UpdateMeters(DownloadProgress p)
        {
            NetworkRateText.Text = FormatRate(p.Network.BytesPerSecond);
            DiskRateText.Text = FormatRate(p.Disk.BytesPerSecond);

            DrawSparkline(NetworkLine, NetworkCanvas, p.Network);
            DrawSparkline(DiskLine, DiskCanvas, p.Disk);
        }

        private static string FormatRate(double bytesPerSecond) =>
            bytesPerSecond < 1024 ? Localization.T("Common_Idle") : $"{Utils.FormatSize((long)bytesPerSecond)}/s";

        private static void DrawSparkline(Polyline line, Canvas canvas, RateMeter meter)
        {
            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            var history = meter.History;

            // Layout may not have run yet the first time the tab is shown
            if (width <= 1 || height <= 1 || history.Count < 2)
            {
                line.Points = new PointCollection();
                return;
            }

            double peak = meter.Peak * 1.15;
            if (peak <= 0)
            {
                line.Points = new PointCollection();
                return;
            }

            var points = new PointCollection(history.Count);
            double step = width / (history.Count - 1);
            for (int i = 0; i < history.Count; i++)
            {
                double scaled = Math.Clamp(history[i] / peak, 0, 1);
                points.Add(new Point(i * step, height - 1 - scaled * (height - 2)));
            }

            points.Freeze();
            line.Points = points;
        }

        private void ResetMeters()
        {
            NetworkRateText.Text = Localization.T("Common_Idle");
            DiskRateText.Text = Localization.T("Common_Idle");
            NetworkLine.Points = new PointCollection();
            DiskLine.Points = new PointCollection();
        }
        #endregion

        /// Reconciles the visible rows with the downloader's latest snapshot. Rows are matched by
        /// asset name and updated in place so the list does not flicker or lose scroll position
        private void SyncActiveRows(IReadOnlyList<FileProgressSnapshot> files)
        {
            for (int i = ActiveDownloads.Count - 1; i >= 0; i--)
            {
                if (!files.Any(f => f.AssetName == ActiveDownloads[i].Key))
                    ActiveDownloads.RemoveAt(i);
            }

            foreach (var file in files)
            {
                var row = ActiveDownloads.FirstOrDefault(r => r.Key == file.AssetName);
                if (row == null)
                {
                    row = new ActiveDownloadRow(file.AssetName);
                    ActiveDownloads.Add(row);
                }
                row.Update(file);
            }

            ActiveFilesEmpty.Visibility = ActiveDownloads.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private static string FormatEta(TimeSpan span) =>
            span.TotalHours >= 1 ? Localization.T("Time_HoursMinutes", (int)span.TotalHours, span.Minutes) :
            span.TotalMinutes >= 1 ? Localization.T("Time_MinutesSeconds", span.Minutes, span.Seconds) :
            Localization.T("Time_Seconds", span.Seconds);

        private static string Shorten(string path) =>
            path.Length <= 70 ? path : "..." + path[^67..];
    }

    public enum DownloadOutcome
    {
        Completed,
        CompletedWithErrors,
        Cancelled,
        Failed,
        Dismissed
    }
}
