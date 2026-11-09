using System.IO;
using System.Text.Json;
using NovaMac.Models;

namespace NovaMac.Services;

public class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public AppSettings Current { get; private set; } = new();

    public AppSettingsService()
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NovaMac");
        Directory.CreateDirectory(baseDir);
        _settingsFilePath = Path.Combine(baseDir, "settings.json");

        Load();
    }

    public AppSettings Load()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    Current = loaded;
                    return Current;
                }
            }
            catch
            {
                // Fallback para configurações padrão
            }
        }

        Current = new AppSettings();
        return Current;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Current = settings;
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }

    public async Task ResetToDefaultsAsync()
    {
        Current = new AppSettings();
        await SaveAsync(Current);
    }
}
