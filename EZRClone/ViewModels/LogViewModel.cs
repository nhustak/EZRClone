using CommunityToolkit.Mvvm.ComponentModel;

namespace EZRClone.ViewModels;

public partial class LogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _placeholderText = "No log entries yet.\n\nThis view will display rclone command output and application logs in a future release.";
}
