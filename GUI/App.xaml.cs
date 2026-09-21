using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Dark.Net;

namespace GUI
{
    public partial class App : Application
    {
        // in case of big oopsies
        private static readonly string CrashLog =
            Path.Combine(AppContext.BaseDirectory, "meibrowser-error.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DarkNet.Instance.SetCurrentProcessTheme(Theme.Dark);

            ConsoleLog.Install();

            // A key present in one translation but not the other is a bug, and this is the
            // cheapest place to find out about it
            Localization.Validate();

            AppSettings.Load();
            Localization.Initialize(AppSettings.SelectedLanguage);

            DispatcherUnhandledException += (_, args) =>
            {
                Record("UI", args.Exception);
                args.Handled = true;
                ThemedDialog.Show(
                    Localization.T("App_UnhandledError", args.Exception.Message, CrashLog),
                    Localization.T("App_UnhandledError_Title"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Record("fatal", args.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Record("background task", args.Exception);
                args.SetObserved();
            };
        }

        private static void Record(string source, Exception? ex)
        {
            if (ex == null) return;

            Console.WriteLine($"Unhandled {source} exception: {ex}");
            try
            {
                File.AppendAllText(CrashLog, $"[{DateTime.Now:u}] {source}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // what do we do now
            }
        }
    }

}
