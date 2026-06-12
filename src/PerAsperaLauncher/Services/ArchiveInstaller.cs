using System.IO;
using System.IO.Compression;

namespace PerAsperaLauncher.Services;

/// <summary>
/// Extraction d'archives au format standard (racine du zip = racine du jeu),
/// avec suivi des fichiers posés pour permettre la désinstallation.
/// </summary>
public static class ArchiveInstaller
{
    /// <summary>
    /// Extrait le zip à la racine du jeu (écrase l'existant) et retourne la liste
    /// des fichiers extraits (chemins relatifs à la racine du jeu).
    /// </summary>
    public static List<string> ExtractToGameRoot(string zipPath, string gamePath)
    {
        var files = new List<string>();
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // dossier

            var relative = entry.FullName.Replace('/', '\\');
            var destPath = Path.GetFullPath(Path.Combine(gamePath, relative));

            // Zip-slip guard : l'entrée doit rester sous la racine du jeu
            if (!destPath.StartsWith(Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
            files.Add(relative);
        }
        return files;
    }

    /// <summary>Supprime les fichiers listés puis les dossiers devenus vides.</summary>
    public static void RemoveFiles(string gamePath, IEnumerable<string> relativeFiles)
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in relativeFiles)
        {
            var path = Path.Combine(gamePath, rel);
            if (File.Exists(path)) File.Delete(path);
            var dir = Path.GetDirectoryName(path);
            if (dir != null) dirs.Add(dir);
        }

        // Remonter les dossiers vides (les plus profonds d'abord)
        foreach (var dir in dirs.OrderByDescending(d => d.Length))
        {
            var current = dir;
            while (current != null
                   && current.StartsWith(gamePath, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(current, gamePath, StringComparison.OrdinalIgnoreCase)
                   && Directory.Exists(current)
                   && !Directory.EnumerateFileSystemEntries(current).Any())
            {
                Directory.Delete(current);
                current = Path.GetDirectoryName(current);
            }
        }
    }
}
