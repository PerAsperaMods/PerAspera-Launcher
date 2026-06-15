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
    private string _statusMessage = "Loading…";
    private int _progressValue;
    private bool _progressVisible;
    private bool _bepInExInstalled;
    private bool _hasInterop;
    private Version? _sdkInstalled;
    private Version? _sdkLatest;
    private GitHubRelease? _sdkLatestRelease;
    private GitHubRelease? _launcherUpdate;

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
        OpenLauncherUpdateCommand = new RelayCommand(OpenLauncherUpdate);
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

    public bool ShowPreReleases
    {
        get => _config.ShowPreReleases;
        set
        {
            if (_config.ShowPreReleases == value) return;
            _config.ShowPreReleases = value;
            ConfigService.Save(_config);
            OnPropertyChanged();
            _ = LoadAsync();
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
    public string GamePathDisplay => GamePath ?? "Game not found — select install folder";

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
        !BepInExInstalled ? "Not installed"
        : !HasInterop ? $"{BepInExInstaller.Version} — launch the game once to generate interop"
        : $"{BepInExInstaller.Version} ✓";

    public string SdkStatus =>
        _sdkInstalled == null && _sdkLatest == null ? "—"
        : _sdkInstalled == null ? $"Not installed (v{_sdkLatest} available)"
        : SdkUpdateAvailable ? $"v{_sdkInstalled} → v{_sdkLatest}"
        : $"v{_sdkInstalled} ✓";

    public bool SdkUpdateAvailable =>
        _sdkLatestRelease != null && (_sdkInstalled == null || (_sdkLatest != null && _sdkLatest > _sdkInstalled));

    public ObservableCollection<ModItemViewModel> Mods { get; } = new();

    public GitHubRelease? LauncherUpdate
    {
        get => _launcherUpdate;
        private set
        {
            if (SetField(ref _launcherUpdate, value))
            {
                OnPropertyChanged(nameof(LauncherUpdateVisible));
                OnPropertyChanged(nameof(LauncherUpdateText));
                OnPropertyChanged(nameof(CurrentVersionText));
            }
        }
    }

    public bool LauncherUpdateVisible => _launcherUpdate != null;

    public string LauncherUpdateText => _launcherUpdate != null
        ? $"Launcher update available: {_launcherUpdate.TagName}"
        : "";

    public string CurrentVersionText => $"v{LauncherUpdateService.GetCurrentVersion()}";

    public RelayCommand BrowseGameCommand { get; }
    public AsyncRelayCommand InstallBepInExCommand { get; }
    public AsyncRelayCommand InstallSdkCommand { get; }
    public AsyncRelayCommand UpdateAllCommand { get; }
    public RelayCommand PlayCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand OpenLauncherUpdateCommand { get; }

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
            StatusMessage = "Select Per Aspera install folder.";
            return;
        }

        // 2) Remote state: SDK + registry + latest release per mod + launcher check
        StatusMessage = "Checking for updates…";
        try
        {
            var sdkTask = SdkManager.GetLatestAsync();
            var registryTask = GitHubClient.GetRegistryAsync();
            var launcherUpdateTask = LauncherUpdateService.CheckForUpdateAsync();

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
            await Task.WhenAll(Mods.Select(async m => m.LatestRelease = await ModManager.GetLatestAsync(m.Entry, ShowPreReleases)));

            LauncherUpdate = await launcherUpdateTask;

            StatusMessage = "Ready.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Offline or network error: {ex.Message}";
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
            if (BepInExInstalled) BepInExInstaller.ApplyDoorstopFix(GamePath!);
        }
        OnPropertyChanged(nameof(SdkStatus));
        OnPropertyChanged(nameof(SdkUpdateAvailable));
    }

    // ----- Actions -----

    private void OpenLauncherUpdate()
        => Process.Start(new ProcessStartInfo(LauncherUpdateService.ReleasesPageUrl) { UseShellExecute = true });

    private void BrowseGame()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Per Aspera install folder (containing 'Per Aspera.exe')"
        };
        if (dialog.ShowDialog() != true) return;

        if (!GameLocator.IsValidGamePath(dialog.FolderName))
        {
            StatusMessage = $"'{GameLocator.GameExeName}' not found in this folder.";
            return;
        }
        _config.GamePath = dialog.FolderName;
        ConfigService.Save(_config);
        GamePath = dialog.FolderName;
        _ = LoadAsync();
    }

    private async Task InstallBepInExAsync()
    {
        await RunWithProgress("Downloading BepInEx…", async p =>
        {
            await BepInExInstaller.InstallAsync(GamePath!, p);
            RefreshLocalState();
            StatusMessage = "BepInEx installed. Launch the game once to generate interop.";
        });
    }

    private async Task InstallSdkAsync()
    {
        if (_sdkLatestRelease == null) return;
        await RunWithProgress($"Downloading SDK {_sdkLatestRelease.TagName}…", async p =>
        {
            await SdkManager.InstallAsync(GamePath!, _sdkLatestRelease, p);
            RefreshLocalState();
            StatusMessage = $"SDK {_sdkLatestRelease.TagName} installed.";
        });
    }

    internal async Task InstallModAsync(ModItemViewModel item)
    {
        if (item.LatestRelease == null) return;

        // SDK dependency: install/update SDK first if required
        if (item.Entry.SdkMinVersion != null && Version.TryParse(item.Entry.SdkMinVersion, out var min)
            && (_sdkInstalled == null || _sdkInstalled < min))
        {
            if (_sdkLatestRelease == null || _sdkLatest == null || _sdkLatest < min)
            {
                StatusMessage = $"{item.Name} requires SDK ≥ {min}, unavailable.";
                return;
            }
            await InstallSdkAsync();
        }

        await RunWithProgress($"Installing {item.Name} {item.LatestRelease.TagName}…", async p =>
        {
            await ModManager.InstallAsync(GamePath!, item.Entry, item.LatestRelease, p);
            item.InstalledVersion = ModManager.GetInstalledVersion(GamePath!, item.Entry.Id);
            StatusMessage = $"{item.Name} {item.LatestRelease.TagName} installed.";
        });
    }

    internal async Task UninstallModAsync(ModItemViewModel item)
    {
        await RunWithProgress($"Uninstalling {item.Name}…", p =>
        {
            ModManager.Uninstall(GamePath!, item.Entry.Id);
            item.InstalledVersion = null;
            StatusMessage = $"{item.Name} uninstalled.";
            return Task.CompletedTask;
        });
    }

    private async Task UpdateAllAsync()
    {
        if (!BepInExInstalled) await InstallBepInExAsync();
        if (SdkUpdateAvailable) await InstallSdkAsync();
        foreach (var mod in Mods.Where(m => m.HasUpdate))
            await InstallModAsync(mod);
        StatusMessage = "Everything is up to date.";
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

        // First launch: watch for interop generation in the background
        if (BepInExInstalled && !HasInterop)
        {
            StatusMessage = "Game launched — generating interop (1-3 min)…";
            _ = Task.Run(async () =>
            {
                var ok = await BepInExInstaller.WaitForInteropAsync(GamePath!, TimeSpan.FromMinutes(10));
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    RefreshLocalState();
                    if (ok) StatusMessage = "Interop generated — mod environment is ready.";
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
            StatusMessage = $"Error: {ex.Message}";
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
