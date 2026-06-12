using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PerAsperaLauncher.Services;

/// <summary>
/// Localise l'installation Steam de Per Aspera (app id 803050) :
/// registre Steam → libraryfolders.vdf → appmanifest → dossier d'install.
/// </summary>
public static class GameLocator
{
    public const string SteamAppId = "803050";
    public const string GameExeName = "Per Aspera.exe";

    /// <summary>Chemin du jeu trouvé automatiquement, ou null.</summary>
    public static string? AutoDetect()
    {
        var steamPath = GetSteamPath();
        if (steamPath == null) return null;

        foreach (var library in GetLibraryFolders(steamPath))
        {
            var manifest = Path.Combine(library, "steamapps", $"appmanifest_{SteamAppId}.acf");
            if (!File.Exists(manifest)) continue;

            var installDir = ReadVdfValue(File.ReadAllText(manifest), "installdir");
            if (installDir == null) continue;

            var gamePath = Path.Combine(library, "steamapps", "common", installDir);
            if (IsValidGamePath(gamePath)) return gamePath;
        }
        return null;
    }

    /// <summary>Le dossier contient bien l'exécutable du jeu.</summary>
    public static bool IsValidGamePath(string? path)
        => !string.IsNullOrEmpty(path) && File.Exists(Path.Combine(path, GameExeName));

    private static string? GetSteamPath()
    {
        // HKCU d'abord (install utilisateur), puis HKLM 32-bit
        var path = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) as string;
        path ??= Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        if (path == null) return null;
        path = path.Replace('/', '\\');
        return Directory.Exists(path) ? path : null;
    }

    private static IEnumerable<string> GetLibraryFolders(string steamPath)
    {
        yield return steamPath; // la bibliothèque par défaut est l'install Steam elle-même

        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;

        // Format VDF : "path"  "D:\\SteamLibrary" (backslashes doublés)
        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
        {
            var lib = m.Groups[1].Value.Replace(@"\\", @"\");
            if (Directory.Exists(lib)) yield return lib;
        }
    }

    private static string? ReadVdfValue(string vdfContent, string key)
    {
        var m = Regex.Match(vdfContent, $"\"{key}\"\\s+\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : null;
    }
}
