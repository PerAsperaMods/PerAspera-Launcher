using System.IO;

namespace PerAsperaLauncher.Services;

/// <summary>
/// Installation de BepInEx 6 IL2CPP (version épinglée be.755, celle validée avec
/// le SDK) et détection de l'interop générée au premier lancement du jeu.
/// </summary>
public static class BepInExInstaller
{
    /// <summary>Build épinglé — même commit (3fab71a) que l'environnement de référence.</summary>
    public const string DownloadUrl =
        "https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755%2B3fab71a.zip";

    public const string Version = "6.0.0-be.755";

    public static bool IsInstalled(string gamePath)
        => File.Exists(Path.Combine(gamePath, "BepInEx", "core", "BepInEx.Unity.IL2CPP.dll"));

    /// <summary>L'interop a été générée (premier lancement du jeu effectué).</summary>
    public static bool HasInterop(string gamePath)
        => File.Exists(Path.Combine(gamePath, "BepInEx", "interop", "ScriptsAssembly.dll"));

    public static async Task InstallAsync(string gamePath, IProgress<int>? progress = null)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "PerAsperaLauncher", "bepinex.zip");
        await GitHubClient.DownloadFileAsync(DownloadUrl, tmp, progress);
        ArchiveInstaller.ExtractToGameRoot(tmp, gamePath);
        File.Delete(tmp);
    }

    /// <summary>
    /// Attend que l'interop apparaisse (générée par BepInEx pendant le premier
    /// lancement du jeu). Retourne true si détectée avant le timeout.
    /// </summary>
    public static async Task<bool> WaitForInteropAsync(string gamePath, TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (HasInterop(gamePath)) return true;
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }
        return HasInterop(gamePath);
    }
}
