using System.Text.Json.Serialization;

namespace PerAsperaLauncher.Models;

/// <summary>Configuration locale du launcher (%AppData%\PerAsperaLauncher\config.json).</summary>
public sealed class LauncherConfig
{
    public string? GamePath { get; set; }
    public bool AdvancedMode { get; set; }
    public string? LastWorkspacePath { get; set; }
}

/// <summary>Racine de registry.json (repo PerAsperaMods/mod-registry).</summary>
public sealed class ModRegistry
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("updated")] public string? Updated { get; set; }
    [JsonPropertyName("sdk")] public SdkInfo? Sdk { get; set; }
    [JsonPropertyName("mods")] public List<ModEntry> Mods { get; set; } = new();
}

public sealed class SdkInfo
{
    [JsonPropertyName("repo")] public string Repo { get; set; } = "";
    [JsonPropertyName("assetPattern")] public string AssetPattern { get; set; } = "";
}

/// <summary>Une entrée de mod du registry.</summary>
public sealed class ModEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("repo")] public string Repo { get; set; } = "";
    [JsonPropertyName("sdkMinVersion")] public string? SdkMinVersion { get; set; }
    [JsonPropertyName("gameVersion")] public string? GameVersion { get; set; }
    [JsonPropertyName("dependencies")] public List<string> Dependencies { get; set; } = new();
}

/// <summary>Manifest local d'un mod installé : permet la désinstallation propre
/// (%AppData%\PerAsperaLauncher\installed\&lt;id&gt;.json).</summary>
public sealed class InstallManifest
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public List<string> Files { get; set; } = new();
}

/// <summary>Release GitHub minimale (champ utiles seulement).</summary>
public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
    [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = new();
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
}
