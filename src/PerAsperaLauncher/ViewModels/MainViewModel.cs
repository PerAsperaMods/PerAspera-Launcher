using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using PerAsperaLauncher.Models;
using PerAsperaLauncher.Services;

namespace PerAsperaLauncher.ViewModels;

/// <summary>État global du launcher et commandes de la fenêtre principale.</summary>
public sealed class MainViewModel : ObservableObject
{
    private LauncherConfig _config = new();
    private string? _gamePath;
    private string _statusMessage = "Initialisation…";
    private int _progressValue;
    private bool _progressVisible;
    private bool _bepInExInstalled;
    private bool _hasInterop;
    private Version? _sdkInstalled;
    private Version? _sdkLatest;
    private GitHubRelease? _sdkLatestRelease;

    public MainViewModel()
    {
        Workspace = new WorkspaceViewModel(() => GamePath, path =>
        {
            _config.LastWorkspacePath = path;
            ConfigService.Save(_config);
        });
        BrowseGameCommand = new RelayCommand(BrowseGame);
        InstallBepInExCommand = new AsyncRelayCommand(InstallBepInExAsync, () => GameFound && !BepInExInstalled);
        InstallSdkCommand = new AsyncRelayCommand(InstallSdkAsync, () => GameFound && BepInExInstalled && SdkUpdateAvailable);
        UpdateAllCommand = new AsyncRelayCommand(UpdateAllAsync, () => GameFound);
        PlayCommand = new RelayCommand(Play, () => GameFound);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
    }

    public WorkspaceViewModel Workspace { get; }

    public bool AdvancedMode
    {
        get => _config.AdvancedMode;
        set
        {
            if (_config.AdvancedMode == value) return;
            _config.AdvancedMode = value;
            ConfigService.Save(_config);
            OnPropertyChanged();
        }
    }

    // ----- Propriétés liées -----

    public string? GamePath
    {
        get => _gamePath;
        private set
        {
            if (SetField(ref _gamePath, value))
            {
                OnPropertyChanged(nameof(GameFound));
                OnPropertyChanged(nameof(GamePathDisplay));
                RaiseAllCanExecute();
            }
        }
    }

    public bool GameFound => GameLocator.IsValidGamePath(GamePath);
    public string GamePathDisplay => GamePath ?? "Jeu introuvable — sélectionnez le dossier d'installation";

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public int ProgressValue { get => _progressValue; set => SetField(ref _progressValue, value); }
    public bool ProgressVisible { get => _progressVisible; set => SetField(ref _progressVisible, value); }

    public bool BepInExInstalled
    {
        get => _bepInExInstalled;
        private set
        {
            if (SetField(ref _bepInExInstalled, value))
            {
                OnPropertyChanged(nameof(BepInExStatus));
                RaiseAllCanExecute();
            }
        }
    }

    public bool HasInterop
    {
        get => _hasInterop;
        private set
        {
            if (SetField(ref _hasInterop, value)) OnPropertyChanged(nameof(BepInExStatus));
        }
    }

    public string BepInExStatus =>
        !BepInExInstalled ? "Non installé"
        : !HasInterop ? $"{BepInExInstaller.Version} — lancez le jeu une fois (génération interop)"
        : $"{BepInExInstaller.Version} ✓";

    public string SdkStatus =>
        _sdkInstalled == null && _sdkLatest == null ? "—"
        : _sdkInstalled == null ? $"Non installé (v{_sdkLatest} disponible)"
        : SdkUpdateAvailable ? $"v{_sdkInstalled} → v{_sdkLatest}"
        : $"v{_sdkInstalled} ✓";

    public bool SdkUpdateAvailable =>
        _sdkLatestRelease != null && (_sdkInstalled == null || (_sdkLatest != null && _sdkLatest > _sdkInstalled));

    public ObservableCollection<ModItemViewModel> Mods { get; } = new();

    public RelayCommand BrowseGameCommand { get; }
    public AsyncRelayCommand InstallBepInExCommand { get; }
    public AsyncRelayCommand InstallSdkCommand { get; }
    public AsyncRelayCommand UpdateAllCommand { get; }
    public RelayCommand PlayCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    // ----- Cycle de vie -----

    public async Task LoadAsync()
    {
        _config = ConfigService.Load();

        // Restaurer le workspace si mémorisé
        if (!string.IsNullOrWhiteSpace(_config.LastWorkspacePath))
            Workspace.WorkspacePath = _config.LastWorkspacePath;

        // 1) Localiser le jeu : config persistée, sinon auto-détection Steam
        var path = GameLocator.IsValidGamePath(_config.GamePath) ? _config.GamePath : GameLocator.AutoDetect();
        if (path != null && _config.GamePath != path)
        {
            _config.GamePath = path;
            ConfigService.Save(_config);
        }
        GamePath = path;

        RefreshLocalState();

        if (!GameFound)
        {
            StatusMessage = "Sélectionnez le dossier d'installation de Per Aspera.";
            return;
        }

        // 2) État distant : SDK + registry + dernière release par mod
        StatusMessage = "Vérification des mises à jour…";
        try
        {
            var sdkTask = SdkManager.GetLatestAsync();
            var registryTask = GitHubClient.GetRegistryAsync();

            var sdk = await sdkTask;
            _sdkLatest = sdk?.Version;
            _sdkLatestRelease = sdk?.Release;
            OnPropertyChanged(nameof(SdkStatus));
            OnPropertyChanged(nameof(SdkUpdateAvailable));

            var registry = await registryTask;
            Mods.Clear();
            foreach (var entry in registry.Mods)
            {
                var item = new ModItemViewModel(this, entry)
                {
                    InstalledVersion = ModManager.GetInstalledVersion(GamePath!, entry.Id)
                };
                Mods.Add(item);
            }

            // Releases en parallèle (peu de mods, API anonyme suffisante)
            await Task.WhenAll(Mods.Select(async m => m.LatestRelease = await ModManager.GetLatestAsync(m.Entry)));

            StatusMessage = "Prêt.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hors-ligne ou erreur réseau : {ex.Message}";
        }
        RaiseAllCanExecute();
    }

    private void RefreshLocalState()
    {
        if (!GameFound)
        {
            BepInExInstalled = false;
            HasInterop = false;
            _sdkInstalled = null;
        }
        else
        {
            BepInExInstalled = BepInExInstaller.IsInstalled(GamePath!);
            HasInterop = BepInExInstaller.HasInterop(GamePath!);
            _sdkInstalled = SdkManager.GetInstalledVersion(GamePath!);
        }
        OnPropertyChanged(nameof(SdkStatus));
        OnPropertyChanged(nameof(SdkUpdateAvailable));
    }

    // ----- Actions -----

    private void BrowseGame()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Dossier d'installation de Per Aspera (contient 'Per Aspera.exe')"
        };
        if (dialog.ShowDialog() != true) return;

        if (!GameLocator.IsValidGamePath(dialog.FolderName))
        {
            StatusMessage = $"'{GameLocator.GameExeName}' introuvable dans ce dossier.";
            return;
        }
        _config.GamePath = dialog.FolderName;
        ConfigService.Save(_config);
        GamePath = dialog.FolderName;
        _ = LoadAsync();
    }

    private async Task InstallBepInExAsync()
    {
        await RunWithProgress("Téléchargement de BepInEx…", async p =>
        {
            await BepInExInstaller.InstallAsync(GamePath!, p);
            RefreshLocalState();
            StatusMessage = "BepInEx installé. Lancez le jeu une fois pour générer l'interop.";
        });
    }

    private async Task InstallSdkAsync()
    {
        if (_sdkLatestRelease == null) return;
        await RunWithProgress($"Téléchargement du SDK {_sdkLatestRelease.TagName}…", async p =>
        {
            await SdkManager.InstallAsync(GamePath!, _sdkLatestRelease, p);
            RefreshLocalState();
            StatusMessage = $"SDK {_sdkLatestRelease.TagName} installé.";
        });
    }

    internal async Task InstallModAsync(ModItemViewModel item)
    {
        if (item.LatestRelease == null) return;

        // Dépendance SDK : installer/mettre à jour le SDK d'abord si requis
        if (item.Entry.SdkMinVersion != null && Version.TryParse(item.Entry.SdkMinVersion, out var min)
            && (_sdkInstalled == null || _sdkInstalled < min))
        {
            if (_sdkLatestRelease == null || _sdkLatest == null || _sdkLatest < min)
            {
                StatusMessage = $"{item.Name} requiert le SDK ≥ {min}, indisponible.";
                return;
            }
            await InstallSdkAsync();
        }

        await RunWithProgress($"Installation de {item.Name} {item.LatestRelease.TagName}…", async p =>
        {
            await ModManager.InstallAsync(GamePath!, item.Entry, item.LatestRelease, p);
            item.InstalledVersion = ModManager.GetInstalledVersion(GamePath!, item.Entry.Id);
            StatusMessage = $"{item.Name} {item.LatestRelease.TagName} installé.";
        });
    }

    internal async Task UninstallModAsync(ModItemViewModel item)
    {
        await RunWithProgress($"Désinstallation de {item.Name}…", p =>
        {
            ModManager.Uninstall(GamePath!, item.Entry.Id);
            item.InstalledVersion = null;
            StatusMessage = $"{item.Name} désinstallé.";
            return Task.CompletedTask;
        });
    }

    private async Task UpdateAllAsync()
    {
        if (!BepInExInstalled) await InstallBepInExAsync();
        if (SdkUpdateAvailable) await InstallSdkAsync();
        foreach (var mod in Mods.Where(m => m.HasUpdate))
            await InstallModAsync(mod);
        StatusMessage = "Tout est à jour.";
    }

    private void Play()
    {
        // steam://run préserve l'overlay Steam ; fallback exe direct hors Steam
        try
        {
            Process.Start(new ProcessStartInfo($"steam://run/{GameLocator.SteamAppId}") { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo(Path.Combine(GamePath!, GameLocator.GameExeName)) { UseShellExecute = true });
        }

        // Premier lancement : surveiller l'apparition de l'interop en arrière-plan
        if (BepInExInstalled && !HasInterop)
        {
            StatusMessage = "Jeu lancé — génération de l'interop en cours (1 à 3 min)…";
            _ = Task.Run(async () =>
            {
                var ok = await BepInExInstaller.WaitForInteropAsync(GamePath!, TimeSpan.FromMinutes(10));
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    RefreshLocalState();
                    if (ok) StatusMessage = "Interop générée — l'environnement de modding est prêt.";
                });
            });
        }
    }

    private async Task RunWithProgress(string message, Func<IProgress<int>, Task> work)
    {
        StatusMessage = message;
        ProgressValue = 0;
        ProgressVisible = true;
        try
        {
            var progress = new Progress<int>(v => ProgressValue = v);
            await work(progress);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
        }
        finally
        {
            ProgressVisible = false;
            RaiseAllCanExecute();
        }
    }

    private void RaiseAllCanExecute()
    {
        InstallBepInExCommand.RaiseCanExecuteChanged();
        InstallSdkCommand.RaiseCanExecuteChanged();
        UpdateAllCommand.RaiseCanExecuteChanged();
        PlayCommand.RaiseCanExecuteChanged();
    }
}
