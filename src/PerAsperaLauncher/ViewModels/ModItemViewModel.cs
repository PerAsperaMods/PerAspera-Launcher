using PerAsperaLauncher.Models;

namespace PerAsperaLauncher.ViewModels;

/// <summary>Une ligne de la liste des mods : entrée registry + état installé/disponible.</summary>
public sealed class ModItemViewModel : ObservableObject
{
    private readonly MainViewModel _parent;
    private string? _installedVersion;
    private GitHubRelease? _latestRelease;

    public ModItemViewModel(MainViewModel parent, ModEntry entry)
    {
        _parent = parent;
        Entry = entry;
        InstallCommand = new AsyncRelayCommand(() => _parent.InstallModAsync(this), () => CanInstall);
        UninstallCommand = new AsyncRelayCommand(() => _parent.UninstallModAsync(this), () => IsInstalled);
    }

    public ModEntry Entry { get; }
    public string Name => Entry.Name;
    public string Description => Entry.Description;
    public string TypeBadge => Entry.Type.ToUpperInvariant();

    public string? InstalledVersion
    {
        get => _installedVersion;
        set
        {
            if (SetField(ref _installedVersion, value)) RefreshDerived();
        }
    }

    public GitHubRelease? LatestRelease
    {
        get => _latestRelease;
        set
        {
            if (SetField(ref _latestRelease, value)) RefreshDerived();
        }
    }

    public string LatestVersion => LatestRelease?.TagName ?? "—";
    public bool IsPreRelease => LatestRelease?.Prerelease == true;
    public bool IsInstalled => InstalledVersion != null;
    public bool HasUpdate => IsInstalled && LatestRelease != null && InstalledVersion != LatestRelease.TagName;
    public bool CanInstall => LatestRelease != null && (!IsInstalled || HasUpdate);

    public string StatusText =>
        !IsInstalled ? "Not installed"
        : HasUpdate ? $"{InstalledVersion} → {LatestVersion}"
        : $"{InstalledVersion} ✓";

    public string InstallButtonText => HasUpdate ? "Update" : "Install";

    public AsyncRelayCommand InstallCommand { get; }
    public AsyncRelayCommand UninstallCommand { get; }

    private void RefreshDerived()
    {
        OnPropertyChanged(nameof(LatestVersion));
        OnPropertyChanged(nameof(IsPreRelease));
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(InstallButtonText));
        InstallCommand.RaiseCanExecuteChanged();
        UninstallCommand.RaiseCanExecuteChanged();
    }
}
