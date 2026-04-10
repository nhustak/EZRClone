using System.Collections.ObjectModel;
using System.Windows;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EZRClone.Models;
using EZRClone.Services;
using RCloneOperationProfile = HotCoreUtility.RClone.RCloneOperationProfile;
using AppRCloneCommandRequest = EZRClone.Models.RCloneCommandRequest;

namespace EZRClone.ViewModels;

public partial class ConfigViewModel : ObservableObject
{
    private static readonly string[] SensitiveKeyMarkers = ["secret", "token", "key", "password"];

    private readonly IRCloneConfigService _configService;
    private readonly IRCloneProcessService _processService;
    private readonly IAppSettingsService _settingsService;

    public ObservableCollection<RCloneRemote> Remotes { get; } = new();
    public ObservableCollection<DisplayPropertyEntry> DisplayProperties { get; } = new();

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

    [ObservableProperty]
    private bool _isConfigPathReady = true;

    public List<RCloneBackendType> BackendTypes { get; } = RCloneBackendType.GetKnownTypes();

    public Action<string>? NavigateToRemoteBrowse { get; set; }
    public Action? NavigateToSettings { get; set; }

    public ConfigViewModel(
        IRCloneConfigService configService,
        IRCloneProcessService processService,
        IAppSettingsService settingsService)
    {
        _configService = configService;
        _processService = processService;
        _settingsService = settingsService;
    }

    partial void OnSelectedRemoteChanged(RCloneRemote? value)
    {
        UpdateDisplayProperties(value);
    }

    [RelayCommand]
    private async Task LoadRemotes()
    {
        var settings = _settingsService.Load();
        ConfigFilePath = settings.RCloneConfigPath;

        if (!EnsureConfigPathReady())
            return;

        StatusMessage = "Loading remotes...";

        var remotes = await Task.Run(() =>
            _configService.ReadConfig(ConfigFilePath)
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());

        Remotes.Clear();
        foreach (var remote in remotes)
            Remotes.Add(remote);

        SelectedRemote ??= Remotes.FirstOrDefault();
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
            SelectedRemote.Properties
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => new PropertyEntry { Key = kvp.Key, Value = kvp.Value }));
        IsEditing = true;
    }

    [RelayCommand]
    private void NewRemote()
    {
        if (!EnsureConfigPathReady())
            return;

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
    private void ToggleSensitiveProperty(DisplayPropertyEntry entry)
    {
        entry.IsRevealed = !entry.IsRevealed;
    }

    [RelayCommand]
    private void Save()
    {
        if (!EnsureConfigPathReady())
            return;

        var validationError = ValidateEditState();
        if (validationError is not null)
        {
            StatusMessage = validationError;
            return;
        }

        var remote = new RCloneRemote
        {
            Name = EditName.Trim(),
            Type = EditType.Trim(),
            Properties = EditProperties
                .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    p => p.Key.Trim(),
                    p => p.Value?.Trim() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
        };

        if (IsNewRemote)
        {
            Remotes.Add(remote);
        }
        else if (SelectedRemote is not null)
        {
            var index = Remotes.IndexOf(SelectedRemote);
            if (index >= 0)
                Remotes[index] = remote;
        }

        RewriteSortedRemotes();
        SelectedRemote = Remotes.FirstOrDefault(r => string.Equals(r.Name, remote.Name, StringComparison.OrdinalIgnoreCase));
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
        if (!EnsureConfigPathReady())
            return;

        var result = MessageBox.Show(
            $"Delete remote '{SelectedRemote.Name}' from {ConfigFilePath}?",
            "Confirm Remote Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var name = SelectedRemote.Name;
        Remotes.Remove(SelectedRemote);
        RewriteSortedRemotes();
        SelectedRemote = Remotes.FirstOrDefault();
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
        if (!EnsureConfigPathReady())
            return;

        try
        {
            StatusMessage = $"Testing '{SelectedRemote.Name}'...";
            var result = await _processService.ExecuteDetailedAsync(new AppRCloneCommandRequest
            {
                Arguments = ["lsd", $"{SelectedRemote.Name}:"],
                Operation = "lsd",
                Category = "Validation",
                OperationProfile = RCloneOperationProfile.RemoteValidation
            });

            if (!result.IsSuccess)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);

            StatusMessage = $"Connection to '{SelectedRemote.Name}' succeeded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
    }

    private bool EnsureConfigPathReady()
    {
        var settings = _settingsService.Load();
        ConfigFilePath = settings.RCloneConfigPath;

        if (string.IsNullOrWhiteSpace(ConfigFilePath) || !File.Exists(ConfigFilePath))
        {
            IsConfigPathReady = false;
            StatusMessage = "Config file path is missing or invalid. Set it in App Settings, then refresh.";
            return false;
        }

        IsConfigPathReady = true;
        return true;
    }

    private string? ValidateEditState()
    {
        if (string.IsNullOrWhiteSpace(EditName))
            return "Remote name is required.";

        if (string.IsNullOrWhiteSpace(EditType))
            return "Remote type is required.";

        var duplicateRemote = Remotes.Any(remote =>
            !ReferenceEquals(remote, SelectedRemote) &&
            string.Equals(remote.Name, EditName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (duplicateRemote)
            return $"A remote named '{EditName.Trim()}' already exists.";

        var propertyKeys = EditProperties
            .Where(p => !string.IsNullOrWhiteSpace(p.Key))
            .Select(p => p.Key.Trim())
            .ToList();

        if (propertyKeys.Count != propertyKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return "Property keys must be unique.";

        return null;
    }

    private void RewriteSortedRemotes()
    {
        var sorted = Remotes.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        Remotes.Clear();
        foreach (var remote in sorted)
            Remotes.Add(remote);

        _configService.WriteConfig(ConfigFilePath, sorted);
    }

    private void UpdateDisplayProperties(RCloneRemote? remote)
    {
        DisplayProperties.Clear();
        if (remote is null)
            return;

        foreach (var property in remote.Properties.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            DisplayProperties.Add(new DisplayPropertyEntry
            {
                Key = property.Key,
                Value = property.Value,
                IsSensitive = SensitiveKeyMarkers.Any(marker =>
                    property.Key.Contains(marker, StringComparison.OrdinalIgnoreCase))
            });
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

public partial class DisplayPropertyEntry : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    private string _value = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    private bool _isSensitive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    private bool _isRevealed;

    public string DisplayValue => !IsSensitive || IsRevealed
        ? Value
        : new string('•', Math.Max(6, Math.Min(Value.Length, 18)));

    public string ToggleLabel => IsRevealed ? "Hide" : "Reveal";
}
