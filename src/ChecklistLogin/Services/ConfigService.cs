using System;
using System.IO;
using System.Text.Json;
using ChecklistLogin.Models;

namespace ChecklistLogin.Services
{
    public static class ConfigService
    {
        private const string ConfigFileName = "config.json";

        public static AppConfig LoadConfig()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(baseDir, ConfigFileName);

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (config != null && !string.IsNullOrWhiteSpace(config.LogFolderPath))
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading config.json: {ex.Message}");
            }

            return new AppConfig();
        }
    }
}
