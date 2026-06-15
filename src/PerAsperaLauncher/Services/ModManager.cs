using System.IO;
using PerAsperaLauncher.Models;

namespace PerAsperaLauncher.Services;

/// <summary>
/// Installation, mise à jour et désinstallation des mods du registry.
/// Chaque installation enregistre un manifest local des fichiers posés,
/// ce qui rend la désinstallation propre (aucune logique par-mod).
/// </summary>
public static class ModManager
{
    /// <summary>
    /// Version installée, ou null si absent. Vérifie la présence d'au moins un fichier
    /// sur disque — nettoie le manifest orphelin si les dossiers ont été supprimés manuellement.
    /// </summary>
    public static string? GetInstalledVersion(string gamePath, string modId)
    {
        var manifest = ConfigService.LoadManifest(modId);
        if (manifest == null) return null;

        // Validation : au moins un fichier du manifest doit exister
        bool anyFile = manifest.Files.Any(f => File.Exists(Path.Combine(gamePath, f)));
        if (!anyFile)
        {
            ConfigService.DeleteManifest(modId);
            return null;
        }
        return manifest.Version;
    }

    public static async Task<GitHubRelease?> GetLatestAsync(ModEntry mod, bool includePreRelease = false)
        => await GitHubClient.GetLatestReleaseAsync(mod.Repo, includePreRelease);

    public static async Task InstallAsync(string gamePath, ModEntry mod, GitHubRelease release,
        IProgress<int>? progress = null)
    {
        var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"{mod.Id} : aucun asset zip dans la release {release.TagName}.");

        var tmp = Path.Combine(Path.GetTempPath(), "PerAsperaLauncher", asset.Name);
        await GitHubClient.DownloadFileAsync(asset.BrowserDownloadUrl, tmp, progress);

        // Réinstallation par-dessus une ancienne version : retirer l'ancienne d'abord
        var previous = ConfigService.LoadManifest(mod.Id);
        if (previous != null)
            ArchiveInstaller.RemoveFiles(gamePath, previous.Files);

        var files = ArchiveInstaller.ExtractToGameRoot(tmp, gamePath);
        File.Delete(tmp);

        ConfigService.SaveManifest(new InstallManifest
        {
            Id = mod.Id,
            Version = release.TagName,
            Files = files
        });
    }

    public static void Uninstall(string gamePath, string modId)
    {
        var manifest = ConfigService.LoadManifest(modId);
        if (manifest == null) return;
        ArchiveInstaller.RemoveFiles(gamePath, manifest.Files);
        ConfigService.DeleteManifest(modId);
    }
}
