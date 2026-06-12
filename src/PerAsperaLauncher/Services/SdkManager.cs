using System.Diagnostics;
using System.IO;
using PerAsperaLauncher.Models;

namespace PerAsperaLauncher.Services;

/// <summary>
/// Installation et mise à jour du SDK PerAspera depuis les releases GitHub
/// (asset joueur : PerAspera-SDK-vX.Y.Z.zip, racine du zip = racine du jeu).
/// </summary>
public static class SdkManager
{
    public const string SdkRepo = "PerAsperaMods/PerAspera-SDK";

    /// <summary>Version installée lue sur la DLL déployée, ou null si absente.</summary>
    public static Version? GetInstalledVersion(string gamePath)
    {
        var dll = Path.Combine(gamePath, "BepInEx", "plugins", "SDK", "PerAspera.ModSDK.dll");
        if (!File.Exists(dll)) return null;
        var info = FileVersionInfo.GetVersionInfo(dll);
        return Version.TryParse(info.FileVersion, out var v) ? Normalize(v) : null;
    }

    /// <summary>Dernière release et version, ou null si introuvable.</summary>
    public static async Task<(Version Version, GitHubRelease Release)?> GetLatestAsync()
    {
        var release = await GitHubClient.GetLatestReleaseAsync(SdkRepo);
        if (release == null) return null;
        var tag = release.TagName.TrimStart('v', 'V');
        return Version.TryParse(tag, out var v) ? (Normalize(v), release) : null;
    }

    public static async Task InstallAsync(string gamePath, GitHubRelease release, IProgress<int>? progress = null)
    {
        // Asset joueur = le zip SDK qui n'est PAS le modder-pack
        var asset = release.Assets.FirstOrDefault(a =>
                        a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                        !a.Name.Contains("modder-pack", StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("Asset SDK joueur introuvable dans la release.");

        var tmp = Path.Combine(Path.GetTempPath(), "PerAsperaLauncher", asset.Name);
        await GitHubClient.DownloadFileAsync(asset.BrowserDownloadUrl, tmp, progress);
        ArchiveInstaller.ExtractToGameRoot(tmp, gamePath);
        File.Delete(tmp);
    }

    /// <summary>FileVersion "1.2.0.0" et tag "1.2.0" doivent comparer égaux.</summary>
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
