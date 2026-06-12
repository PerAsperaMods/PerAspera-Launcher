using System.IO;
using System.Net.Http;
using System.Text.Json;
using PerAsperaLauncher.Models;

namespace PerAsperaLauncher.Services;

/// <summary>
/// Accès GitHub anonyme : registry brut (raw.githubusercontent, pas de rate-limit)
/// et releases via l'API REST (60 req/h non authentifié — suffisant, peu de mods).
/// </summary>
public static class GitHubClient
{
    public const string RegistryUrl =
        "https://raw.githubusercontent.com/PerAsperaMods/mod-registry/main/registry.json";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        // L'API GitHub exige un User-Agent
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PerAsperaLauncher/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.Timeout = TimeSpan.FromMinutes(10); // downloads de ~30 MB inclus
        return client;
    }

    public static async Task<ModRegistry> GetRegistryAsync()
    {
        var json = await Http.GetStringAsync(RegistryUrl);
        return JsonSerializer.Deserialize<ModRegistry>(json)
               ?? throw new InvalidOperationException("registry.json invalide.");
    }

    public static async Task<GitHubRelease?> GetLatestReleaseAsync(string repo)
    {
        using var response = await Http.GetAsync($"https://api.github.com/repos/{repo}/releases/latest");
        if (!response.IsSuccessStatusCode) return null; // pas de release (404) ou rate-limit
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GitHubRelease>(json);
    }

    /// <summary>Télécharge un fichier avec progression (0-100).</summary>
    public static async Task DownloadFileAsync(string url, string destPath, IProgress<int>? progress = null)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var dest = File.Create(destPath);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await source.ReadAsync(buffer)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, n));
            read += n;
            if (total > 0) progress?.Report((int)(read * 100 / total));
        }
    }
}
