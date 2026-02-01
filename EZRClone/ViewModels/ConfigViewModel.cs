using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EZRClone.Models;
using EZRClone.Services;

namespace EZRClone.ViewModels;

public partial class ConfigViewModel : ObservableObject
{
    private readonly IRCloneConfigService _configService;
    private readonly IRCloneProcessService _processService;
    private readonly IAppSettingsService _settingsService;

    public ObservableCollection<RCloneRemote> Remotes { get; } = new();

    [ObservableProperty]
    private RCloneRemote? _selectedRemote;

    [ObservableProperty]
    private string _configFilePath = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editType = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PropertyEntry> _editProperties = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isNewRemote;

    public List<RCloneBackendType> BackendTypes { get; } = RCloneBackendType.GetKnownTypes();

    public Action<string>? NavigateToRemoteBrowse { get; set; }

    public ConfigViewModel(
        IRCloneConfigService configService,
        IRCloneProcessService processService,
        IAppSettingsService settingsService)
    {
        _configService = configService;
        _processService = processService;
        _settingsService = settingsService;
    }

    [RelayCommand]
    private void LoadRemotes()
    {
        var settings = _settingsService.Load();
        ConfigFilePath = settings.RCloneConfigPath;

        if (string.IsNullOrWhiteSpace(ConfigFilePath))
        {
            StatusMessage = "Config file path not set. Configure in Settings.";
            return;
        }

        Remotes.Clear();
        var remotes = _configService.ReadConfig(ConfigFilePath);
        foreach (var r in remotes)
            Remotes.Add(r);

        StatusMessage = $"Loaded {Remotes.Count} remote(s).";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedRemote is null) return;

        IsNewRemote = false;
        EditName = SelectedRemote.Name;
        EditType = SelectedRemote.Type;
        EditProperties = new ObservableCollection<PropertyEntry>(
            SelectedRemote.Properties.Select(kvp => new PropertyEntry { Key = kvp.Key, Value = kvp.Value }));
        IsEditing = true;
    }

    [RelayCommand]
    private void NewRemote()
    {
        IsNewRemote = true;
        EditName = string.Empty;
        EditType = "s3";
        EditProperties = new ObservableCollection<PropertyEntry>();
        IsEditing = true;
    }

    [RelayCommand]
    private void AddProperty()
    {
        EditProperties.Add(new PropertyEntry());
    }

    [RelayCommand]
    private void RemoveProperty(PropertyEntry entry)
    {
        EditProperties.Remove(entry);
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "Remote name is required.";
            return;
        }

        var remote = new RCloneRemote
        {
            Name = EditName.Trim(),
            Type = EditType,
            Properties = EditProperties
                .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                .ToDictionary(p => p.Key.Trim(), p => p.Value?.Trim() ?? string.Empty)
        };

        if (IsNewRemote)
        {
            Remotes.Add(remote);
        }
        else if (SelectedRemote is not null)
        {
            var index = Remotes.IndexOf(SelectedRemote);
            if (index >= 0)
            {
                Remotes[index] = remote;
                SelectedRemote = remote;
            }
        }

        _configService.WriteConfig(ConfigFilePath, Remotes.ToList());
        IsEditing = false;
        StatusMessage = $"Saved remote '{remote.Name}'.";
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedRemote is null) return;

        var name = SelectedRemote.Name;
        Remotes.Remove(SelectedRemote);
        _configService.WriteConfig(ConfigFilePath, Remotes.ToList());
        SelectedRemote = null;
        StatusMessage = $"Deleted remote '{name}'.";
    }

    [RelayCommand]
    private void BrowseRemote()
    {
        if (SelectedRemote is null) return;
        NavigateToRemoteBrowse?.Invoke(SelectedRemote.Name);
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (SelectedRemote is null) return;

        try
        {
            StatusMessage = $"Testing '{SelectedRemote.Name}'...";
            var result = await _processService.RunAsync($"lsd {SelectedRemote.Name}:");
            StatusMessage = $"Connection to '{SelectedRemote.Name}' succeeded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
    }
}

public partial class PropertyEntry : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}
