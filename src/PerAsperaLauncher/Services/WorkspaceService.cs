using System.IO;
using System.IO.Compression;
using System.Text;

namespace PerAsperaLauncher.Services;

/// <summary>
/// Crée et maintient un workspace Claude Code pour le modding Per Aspera :
///   - .claude/settings.json  (marketplace per-aspera enregistré)
///   - .claude/skills/        (copie directe depuis per-aspera-skills, fonctionne VSCode + CLI)
///   - .claude/agents/
///   - CLAUDE.md              (chemin jeu injecté)
/// Et clone les templates de mods (YAML ou C# avec SDK modder-pack).
/// </summary>
public static class WorkspaceService
{
    public const string SkillsRepo        = "PerAsperaMods/per-aspera-skills";
    public const string TemplateYamlRepo  = "PerAsperaMods/mod-template-yaml";
    public const string TemplateCSharpRepo = "PerAsperaMods/mod-template-csharp";

    private static string ZipUrl(string repo) =>
        $"https://github.com/{repo}/archive/refs/heads/master.zip";

    // ── settings.json Claude Code ──────────────────────────────────────────────
    // Enregistre le marketplace (utile pour les utilisateurs CLI/desktop).
    // Les skills sont aussi copiées directement (fonctionne partout, y compris VSCode).
    private const string ClaudeSettings = """
        {
          "extraKnownMarketplaces": [
            {
              "name": "per-aspera",
              "owner": "PerAsperaMods",
              "repo": "per-aspera-skills"
            }
          ]
        }
        """;

    // ── Init workspace ──────────────────────────────────────────────────────────

    /// <summary>
    /// Crée (ou réinitialise) le workspace : structure .claude/ + skills + CLAUDE.md.
    /// </summary>
    public static async Task InitWorkspaceAsync(
        string workspacePath, string? gamePath, IProgress<int>? progress = null)
    {
        Directory.CreateDirectory(workspacePath);

        var claudeDir = Path.Combine(workspacePath, ".claude");
        Directory.CreateDirectory(claudeDir);

        // UTF-8 sans BOM : requis pour que Claude Code lise les frontmatters correctement
        await WriteNoBomAsync(Path.Combine(claudeDir, "settings.json"), ClaudeSettings);
        progress?.Report(5);

        await InstallSkillsAsync(workspacePath, new Progress<int>(p => progress?.Report(5 + p * 85 / 100)));

        var gameLine = gamePath != null ? $"\nGame install: `{gamePath}`\n" : "";
        await WriteNoBomAsync(Path.Combine(workspacePath, "CLAUDE.md"),
            $"# Per Aspera Mod Workspace\n{gameLine}\nSkills installées via Per Aspera Mod Launcher.\n");

        progress?.Report(100);
    }

    /// <summary>Re-télécharge et réinstalle les skills/agents depuis per-aspera-skills.</summary>
    public static async Task UpdateSkillsAsync(string workspacePath, IProgress<int>? progress = null)
        => await InstallSkillsAsync(workspacePath, progress);

    // ── Templates ───────────────────────────────────────────────────────────────

    /// <summary>Clone le template YAML dans workspacePath/modId/.</summary>
    public static async Task CreateYamlModAsync(
        string workspacePath, string modId, IProgress<int>? progress = null)
    {
        var tmp = TempPath("mod-template-yaml.zip");
        await GitHubClient.DownloadFileAsync(ZipUrl(TemplateYamlRepo), tmp,
            new Progress<int>(p => progress?.Report(p * 90 / 100)));

        var dest = Path.Combine(workspacePath, modId);
        ExtractTemplateZip(tmp, "mod-template-yaml-master", dest);
        File.Delete(tmp);

        // Personnaliser le modId dans manifest.yaml
        var manifest = Path.Combine(dest, "manifest.yaml");
        if (File.Exists(manifest))
        {
            var txt = await File.ReadAllTextAsync(manifest);
            txt = txt.Replace("modId: my-mod", $"modId: {modId}");
            await WriteNoBomAsync(manifest, txt);
        }

        progress?.Report(100);
    }

    /// <summary>Clone le template C# dans workspacePath/modId/ + SDK modder-pack dans lib/SDK/.</summary>
    public static async Task CreateCSharpModAsync(
        string workspacePath, string modId, IProgress<int>? progress = null)
    {
        // 1) Template C#
        var tmp = TempPath("mod-template-csharp.zip");
        await GitHubClient.DownloadFileAsync(ZipUrl(TemplateCSharpRepo), tmp,
            new Progress<int>(p => progress?.Report(p * 35 / 100)));

        var dest = Path.Combine(workspacePath, modId);
        ExtractTemplateZip(tmp, "mod-template-csharp-master", dest);
        File.Delete(tmp);

        RenameModFiles(dest, "MyMod", modId);
        await PatchCSharpFilesAsync(dest, modId);
        progress?.Report(40);

        // 2) SDK modder-pack → lib/SDK/
        var sdkRelease = await GitHubClient.GetLatestReleaseAsync(SdkManager.SdkRepo);
        if (sdkRelease != null)
        {
            var asset = sdkRelease.Assets.FirstOrDefault(a =>
                a.Name.Contains("modder-pack", StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (asset != null)
            {
                var sdkTmp = TempPath(asset.Name);
                await GitHubClient.DownloadFileAsync(asset.BrowserDownloadUrl, sdkTmp,
                    new Progress<int>(p => progress?.Report(40 + p * 55 / 100)));

                var libSdk = Path.Combine(dest, "lib", "SDK");
                Directory.CreateDirectory(libSdk);
                ExtractFlatInto(sdkTmp, libSdk);
                File.Delete(sdkTmp);
            }
        }

        progress?.Report(100);
    }

    // ── Internals ───────────────────────────────────────────────────────────────

    private static async Task InstallSkillsAsync(string workspacePath, IProgress<int>? progress)
    {
        var tmp = TempPath("per-aspera-skills.zip");
        var url = ZipUrl(SkillsRepo);
        await GitHubClient.DownloadFileAsync(url, tmp,
            new Progress<int>(p => progress?.Report(p * 95 / 100)));

        CopySkillsFromZip(tmp, workspacePath);
        File.Delete(tmp);
        progress?.Report(100);
    }

    private static void CopySkillsFromZip(string zipPath, string workspacePath)
    {
        var skillsDir = Path.Combine(workspacePath, ".claude", "skills");
        var agentsDir = Path.Combine(workspacePath, ".claude", "agents");
        Directory.CreateDirectory(skillsDir);
        Directory.CreateDirectory(agentsDir);

        const string skillsPrefix = "per-aspera-skills-master/plugins/per-aspera-modding/skills/";
        const string agentsPrefix = "per-aspera-skills-master/plugins/per-aspera-modding/agents/";

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');

            if (name.StartsWith(skillsPrefix, StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                // per-aspera-skills-master/plugins/.../skills/{skillName}/SKILL.md
                var rel = name[skillsPrefix.Length..];
                var parts = rel.Split('/');
                if (parts.Length != 2) continue;
                var skillDir = Path.Combine(skillsDir, parts[0]);
                Directory.CreateDirectory(skillDir);
                // SKILL.md → skill.md (format local Claude Code)
                entry.ExtractToFile(Path.Combine(skillDir, "skill.md"), overwrite: true);
            }
            else if (name.StartsWith(agentsPrefix, StringComparison.OrdinalIgnoreCase)
                     && name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                entry.ExtractToFile(Path.Combine(agentsDir, Path.GetFileName(name)), overwrite: true);
            }
        }
    }

    private static void ExtractTemplateZip(string zipPath, string zipRoot, string destDir)
    {
        Directory.CreateDirectory(destDir);
        var prefix = zipRoot.TrimEnd('/') + "/";

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var relative = name[prefix.Length..];
            if (string.IsNullOrEmpty(relative) || relative.EndsWith('/')) continue;

            var dest = Path.GetFullPath(Path.Combine(destDir, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!dest.StartsWith(Path.GetFullPath(destDir), StringComparison.OrdinalIgnoreCase)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, overwrite: true);
        }
    }

    // Extrait à plat (par nom de fichier uniquement) — idéal pour les DLLs du modder-pack
    private static void ExtractFlatInto(string zipPath, string destDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            entry.ExtractToFile(Path.Combine(destDir, entry.Name), overwrite: true);
        }
    }

    private static void RenameModFiles(string dir, string oldName, string newName)
    {
        foreach (var file in Directory.GetFiles(dir, $"*{oldName}*", SearchOption.AllDirectories))
        {
            var renamed = file.Replace(oldName, newName);
            if (file != renamed) File.Move(file, renamed);
        }
    }

    private static async Task PatchCSharpFilesAsync(string dir, string modId)
    {
        // GUID safe : minuscules, alphanumérique uniquement
        var safeId = new string(modId.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        foreach (var file in Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly)
                     .Concat(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)))
        {
            var txt = await File.ReadAllTextAsync(file);
            txt = txt.Replace("com.peraspera.author.mymod", $"com.peraspera.author.{safeId}")
                     .Replace("\"MyMod\"", $"\"{modId}\"");
            await WriteNoBomAsync(file, txt);
        }
    }

    private static string TempPath(string name) =>
        Path.Combine(Path.GetTempPath(), "PerAsperaLauncher", name);

    // UTF-8 sans BOM — même principe que sync-skills.ps1 (BOM casse le frontmatter Claude Code)
    private static async Task WriteNoBomAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
