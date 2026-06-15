using System.Reflection;
using PerAsperaLauncher.Models;

namespace PerAsperaLauncher.Services;

public static class LauncherUpdateService
{
    public const string LauncherRepo = "PerAsperaMods/PerAspera-Launcher";
    public const string ReleasesPageUrl = "https://github.com/PerAsperaMods/PerAspera-Launcher/releases/latest";

    public static Version GetCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, v.Build);
    }

    /// <summary>Retourne la release disponible si elle est plus récente que la version locale, sinon null.</summary>
    public static async Task<GitHubRelease?> CheckForUpdateAsync()
    {
        var latest = await GitHubClient.GetLatestReleaseAsync(LauncherRepo);
        if (latest == null) return null;

        var tagStr = latest.TagName.TrimStart('v');
        if (!Version.TryParse(tagStr, out var latestVer)) return null;

        return latestVer > GetCurrentVersion() ? latest : null;
    }
}
