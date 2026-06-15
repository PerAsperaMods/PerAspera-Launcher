using System.IO;
using PerAsperaLauncher.Services;

namespace PerAsperaLauncher.ViewModels;

/// <summary>
/// ViewModel pour l'onglet Développeur : workspace Claude + templates de mods.
/// </summary>
public sealed class WorkspaceViewModel : ObservableObject
{
    private string _workspacePath = "";
    private string _modIdInput = "my-mod";
    private string _statusMessage = "";
    private int _progressValue;
    private bool _progressVisible;

    private readonly Func<string?> _getGamePath;
    private readonly Action<string>? _onWorkspacePathChanged;

    public WorkspaceViewModel(Func<string?> getGamePath, Action<string>? onWorkspacePathChanged = null)
    {
        _getGamePath = getGamePath;
        _onWorkspacePathChanged = onWorkspacePathChanged;

        BrowseWorkspaceCommand = new RelayCommand(BrowseWorkspace);
        InitWorkspaceCommand = new AsyncRelayCommand(
            InitWorkspaceAsync,
            () => !string.IsNullOrWhiteSpace(_workspacePath));
        UpdateSkillsCommand = new AsyncRelayCommand(
            UpdateSkillsAsync,
            () => WorkspaceInitialized);
        NewYamlModCommand = new AsyncRelayCommand(
            NewYamlModAsync,
            () => WorkspaceInitialized && !string.IsNullOrWhiteSpace(_modIdInput));
        NewCSharpModCommand = new AsyncRelayCommand(
            NewCSharpModAsync,
            () => WorkspaceInitialized && !string.IsNullOrWhiteSpace(_modIdInput));
    }

    // ----- Propriétés -----

    public string WorkspacePath
    {
        get => _workspacePath;
        set
        {
            if (SetField(ref _workspacePath, value))
            {
                RaiseAllCanExecute();
                if (!string.IsNullOrWhiteSpace(value)) _onWorkspacePathChanged?.Invoke(value);
            }
        }
    }

    public string ModIdInput
    {
        get => _modIdInput;
        set { if (SetField(ref _modIdInput, value)) RaiseAllCanExecute(); }
    }

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public int ProgressValue { get => _progressValue; set => SetField(ref _progressValue, value); }
    public bool ProgressVisible { get => _progressVisible; set => SetField(ref _progressVisible, value); }

    private bool WorkspaceInitialized =>
        !string.IsNullOrWhiteSpace(_workspacePath)
        && Directory.Exists(Path.Combine(_workspacePath, ".claude"));

    // ----- Commandes -----

    public RelayCommand BrowseWorkspaceCommand { get; }
    public AsyncRelayCommand InitWorkspaceCommand { get; }
    public AsyncRelayCommand UpdateSkillsCommand { get; }
    public AsyncRelayCommand NewYamlModCommand { get; }
    public AsyncRelayCommand NewCSharpModCommand { get; }

    // ----- Actions -----

    private void BrowseWorkspace()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Per Aspera workspace folder (Claude modding directory)"
        };
        if (dialog.ShowDialog() != true) return;
        WorkspacePath = dialog.FolderName;
    }

    private async Task InitWorkspaceAsync()
    {
        await RunWithProgress("Initializing workspace…", async p =>
        {
            await WorkspaceService.InitWorkspaceAsync(_workspacePath, _getGamePath(), p);
            RaiseAllCanExecute();
            StatusMessage = "Workspace initialized — Claude skills and agents installed.";
        });
    }

    private async Task UpdateSkillsAsync()
    {
        await RunWithProgress("Updating skills…", async p =>
        {
            await WorkspaceService.UpdateSkillsAsync(_workspacePath, p);
            StatusMessage = "Skills and agents updated.";
        });
    }

    private async Task NewYamlModAsync()
    {
        var modId = _modIdInput.Trim();
        await RunWithProgress($"Creating YAML mod '{modId}'…", async p =>
        {
            await WorkspaceService.CreateYamlModAsync(_workspacePath, modId, p);
            StatusMessage = $"YAML mod '{modId}' created in {Path.Combine(_workspacePath, modId)}";
        });
    }

    private async Task NewCSharpModAsync()
    {
        var modId = _modIdInput.Trim();
        await RunWithProgress($"Creating C# plugin '{modId}' + SDK…", async p =>
        {
            await WorkspaceService.CreateCSharpModAsync(_workspacePath, modId, p);
            StatusMessage = $"C# plugin '{modId}' created in {Path.Combine(_workspacePath, modId)}";
        });
    }

    // ----- Helpers -----

    private async Task RunWithProgress(string message, Func<IProgress<int>, Task> work)
    {
        StatusMessage = message;
        ProgressValue = 0;
        ProgressVisible = true;
        try
        {
            await work(new Progress<int>(v => ProgressValue = v));
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
        InitWorkspaceCommand.RaiseCanExecuteChanged();
        UpdateSkillsCommand.RaiseCanExecuteChanged();
        NewYamlModCommand.RaiseCanExecuteChanged();
        NewCSharpModCommand.RaiseCanExecuteChanged();
    }
}
