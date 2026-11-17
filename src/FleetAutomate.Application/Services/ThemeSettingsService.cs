using System;
using System.IO;
using System.Text.Json;

namespace FleetAutomate.Services;

public sealed class ThemeSettingsService
{
    private readonly string _settingsDirectory;
    private readonly string _settingsFilePath;

    public ThemeSettingsService()
    {
        _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Canvas.TestRunner");

        _settingsFilePath = Path.Combine(_settingsDirectory, "theme-settings.json");
    }

    public string? LoadAccentColor()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<ThemeSettings>(json)?.AccentColor;
        }
        catch
        {
            return null;
        }
    }

    public void SaveAccentColor(string? accentColor)
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);
            var settings = new ThemeSettings { AccentColor = accentColor };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Theme persistence should never prevent the UI from applying the chosen color.
        }
    }

    private sealed class ThemeSettings
    {
        public string? AccentColor { get; set; }
    }
}
