# Per Aspera Mod Launcher

Install and update the full **Per Aspera** modding environment in a few clicks:

- **BepInEx 6 IL2CPP** (pinned validated build) — complete bootstrap from a vanilla game
- **PerAspera SDK** — from the [official releases](https://github.com/PerAsperaMods/PerAspera-SDK/releases)
- **Community mods** — from the curated [mod-registry](https://github.com/PerAsperaMods/mod-registry)

## Installation

1. Download `PerAsperaLauncher.zip` from the [latest release](../../releases/latest)
2. Extract and run `PerAsperaLauncher.exe` — no installation, no prerequisites
3. Steam game path is detected automatically; use **Browse…** if not found

## First-time setup from a vanilla game

**Install BepInEx** → **▶ PLAY** once (interop generation, 1–3 min, the launcher monitors this) → **Install / Update SDK** → Install your mods → **▶ PLAY**

## How it works

- Installed mods are tracked in `%AppData%\PerAsperaLauncher\` (per-mod file manifest → clean uninstall)
- Only **stable** releases are shown (latest GitHub release per mod)
- C# mods that require a minimum SDK version automatically trigger an SDK update

## Build from source

```powershell
dotnet publish src/PerAsperaLauncher/PerAsperaLauncher.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

.NET 8, WPF, zero NuGet runtime dependencies.

## Publishing your mod in the launcher

See [mod-registry/CONVENTIONS.md](https://github.com/PerAsperaMods/mod-registry/blob/main/CONVENTIONS.md) —
a reusable workflow publishes your release in the correct format, then a PR adds your mod to the registry.

---

*[Français]* Voir [README-fr.md](README-fr.md) pour la documentation en français.
