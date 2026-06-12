# Per Aspera Mod Launcher

Installe et met à jour tout l'environnement de modding **Per Aspera** en quelques clics :

- **BepInEx 6 IL2CPP** (version épinglée validée) — bootstrap complet depuis un jeu vanilla
- **PerAspera SDK** — depuis les [releases officielles](https://github.com/PerAsperaMods/PerAspera-SDK/releases)
- **Mods** de la communauté — depuis le [mod-registry](https://github.com/PerAsperaMods/mod-registry) curé

![screenshot](docs/screenshot.png)

## Installation

1. Téléchargez `PerAsperaLauncher.zip` depuis la [dernière release](../../releases/latest)
2. Extrayez et lancez `PerAsperaLauncher.exe` (aucune installation, aucun prérequis)
3. Le jeu Steam est détecté automatiquement ; sinon, bouton **Parcourir…**

## Premier setup depuis un jeu vanilla

1. **Installer** BepInEx → 2. **▶ JOUER** une fois (génération de l'interop, 1-3 min,
le launcher surveille) → 3. **Installer / MAJ** le SDK → 4. Installer vos mods → 5. **▶ JOUER**

## Fonctionnement

- Les mods installés sont suivis dans `%AppData%\PerAsperaLauncher\` (manifest des
  fichiers posés par mod → désinstallation propre, bouton ✕)
- Le launcher ne montre que les versions **stables** (latest release GitHub de chaque mod)
- Les mods C# qui requièrent le SDK (`sdkMinVersion`) déclenchent sa mise à jour automatiquement

## Build depuis les sources

```powershell
dotnet publish src/PerAsperaLauncher/PerAsperaLauncher.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

.NET 8, WPF, zéro dépendance NuGet.

## Publier votre mod dans le launcher

Voir [mod-registry/CONVENTIONS.md](https://github.com/PerAsperaMods/mod-registry/blob/main/CONVENTIONS.md) —
un workflow réutilisable publie votre release au bon format, puis une PR ajoute votre mod au registry.
