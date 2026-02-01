using CommunityToolkit.Mvvm.ComponentModel;

namespace EZRClone.Models;

public partial class RemoteItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplaySize))]
    private long _size;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayModTime))]
    private DateTime? _modTime;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayStatus))]
    private bool _isLoadingInfo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplaySize))]
    [NotifyPropertyChangedFor(nameof(DisplayFileCount))]
    private bool _hasLoadedInfo;

    public string DisplaySize => IsDirectory
        ? (HasLoadedInfo ? FormatSize(Size) : "")
        : FormatSize(Size);

    public string DisplayStatus => IsLoadingInfo ? "⏳" : "";

    public string DisplayModTime => ModTime?.ToString("g") ?? "";

    public string DisplayFileCount => IsDirectory && HasLoadedInfo
        ? $"{FileCount:N0} total"
        : "";

    partial void OnFileCountChanged(int value) => OnPropertyChanged(nameof(DisplayFileCount));

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return "";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
