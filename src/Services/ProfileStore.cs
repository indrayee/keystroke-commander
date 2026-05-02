using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using KeystrokeCommander.Models;

namespace KeystrokeCommander.Services;

public class ProfileStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _opts;

    public ProfileStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeystrokeCommander");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "profiles.json");
        _opts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings { Profiles = { new Profile { Name = "Default" } } };
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<AppSettings>(json, _opts) ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _opts);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _opts);
        File.WriteAllText(_filePath, json);
    }
}
