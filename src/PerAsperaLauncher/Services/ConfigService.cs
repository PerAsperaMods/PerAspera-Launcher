using System.IO;
using System.Text.Json;
using PerAsperaLauncher.Models;

namespace PerAsperaLauncher.Services;

/// <summary>
/// Persistance locale : config (chemin du jeu) et manifests d'installation des mods,
/// dans %AppData%\PerAsperaLauncher\.
/// </summary>
public static class ConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static string RootDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PerAsperaLauncher");

    private static string ConfigPath => Path.Combine(RootDir, "config.json");
    private static string InstalledDir => Path.Combine(RootDir, "installed");

    public static LauncherConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(ConfigPath)) ?? new();
        }
        catch { /* config corrompue → repartir de zéro */ }
        return new LauncherConfig();
    }

    public static void Save(LauncherConfig config)
    {
        Directory.CreateDirectory(RootDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOpts));
    }

    public static InstallManifest? LoadManifest(string modId)
    {
        var path = Path.Combine(InstalledDir, $"{modId}.json");
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(path));
        }
        catch { }
        return null;
    }

    public static void SaveManifest(InstallManifest manifest)
    {
        Directory.CreateDirectory(InstalledDir);
        File.WriteAllText(Path.Combine(InstalledDir, $"{manifest.Id}.json"),
            JsonSerializer.Serialize(manifest, JsonOpts));
    }

    public static void DeleteManifest(string modId)
    {
        var path = Path.Combine(InstalledDir, $"{modId}.json");
        if (File.Exists(path)) File.Delete(path);
    }
}
