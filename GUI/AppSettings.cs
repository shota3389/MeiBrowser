using System;
using System.IO;
using System.Text.Json;

namespace GUI
{
    public static class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeiBrowser", "settings.json");

        public static string SelectedTheme { get; set; } = "Dark";

        /// <summary>Language the interface starts in. Defaults to Chinese for a fresh install.</summary>
        public static AppLanguage SelectedLanguage { get; set; } = AppLanguage.Chinese;

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var data = JsonSerializer.Deserialize<SettingsData>(json);
                    if (data != null)
                    {
                        SelectedTheme = data.SelectedTheme ?? "Dark";

                        // An unknown or absent value (older settings file) keeps the default
                        if (Enum.TryParse<AppLanguage>(data.SelectedLanguage, ignoreCase: true, out var language))
                            SelectedLanguage = language;
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir!);

                var data = new SettingsData
                {
                    SelectedTheme = SelectedTheme,
                    SelectedLanguage = SelectedLanguage.ToString()
                };
                var json = JsonSerializer.Serialize(data);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }

        private class SettingsData
        {
            public string? SelectedTheme { get; set; }
            public string? SelectedLanguage { get; set; }
        }
    }
}
