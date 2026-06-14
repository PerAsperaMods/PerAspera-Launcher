# Launcher — Contexte développement

WPF .NET 8 — Per Aspera Mod Launcher (GUI joueur).
Voir [CLAUDE.md](../CLAUDE.md) pour le contexte global.

**Repo GitHub séparé :** `https://github.com/PerAsperaMods/PerAspera-Launcher`

## Build

```powershell
# Build local (développement)
dotnet build F:\ModPeraspera\Launcher\src\PerAsperaLauncher\PerAsperaLauncher.csproj -c Release

# Publish self-contained (= ce que fait la CI au tag v*)
dotnet publish F:\ModPeraspera\Launcher\src\PerAsperaLauncher\PerAsperaLauncher.csproj `
  -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

CI/CD : `.github\workflows\release.yml` — déclenché par tag `v*` → ZIP `PerAsperaLauncher-vX.Y.Z.zip`

## ⚠️ Isolation MSBuild CRITIQUE

`Launcher\Directory.Build.props` et `Launcher\Directory.Build.targets` **bloquent** l'héritage MSBuild racine.

**Ne JAMAIS supprimer ces fichiers.** Sans eux :
- Les DLLs du jeu (`gamelibs\`) se retrouvent dans le bin du Launcher
- `BepInEx.AssemblyPublicizer` s'applique au projet WPF
- Le build échoue ou produit un binaire corrompu

## Services architecture

| Service | Rôle |
|---------|------|
| `GameLocator` | Steam registry → vdf → acf → chemin d'installation |
| `BepInExInstaller` | Détecte / installe BepInEx dans le dossier jeu |
| `SdkManager` | `FileVersionInfo` sur `PerAspera.ModSDK.dll` — version SDK détectée |
| `ModManager` | Manifest AppData + validation filesystem |

## Manifest mods

Mods détectés via `%AppData%\PerAsperaLauncher\installed\<id>.json`.

Si des fichiers mod sont supprimés manuellement, le manifest orphelin est auto-nettoyé au prochain `LoadAsync`.
