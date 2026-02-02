using System.Windows;
using System.Windows.Controls;
using EZRClone.ViewModels;
using Microsoft.Win32;

namespace EZRClone.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.LoadCommand.Execute(null);
        }
    }

    private void OnBrowseExe(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select rclone.exe",
            Filter = "Executable files (*.exe)|*.exe",
            FileName = "rclone.exe"
        };

        if (dialog.ShowDialog() == true && DataContext is SettingsViewModel vm)
        {
            vm.RCloneExePath = dialog.FileName;
        }
    }

    private void OnBrowseConfig(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select rclone.conf",
            Filter = "Config files (*.conf)|*.conf|All files (*.*)|*.*",
            FileName = "rclone.conf"
        };

        if (dialog.ShowDialog() == true && DataContext is SettingsViewModel vm)
        {
            vm.RCloneConfigPath = dialog.FileName;
        }
    }

    private void OnBrowseDownloadPath(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select default download folder"
        };

        if (dialog.ShowDialog() == true && DataContext is SettingsViewModel vm)
        {
            vm.DefaultDownloadPath = dialog.FolderName;
        }
    }
}
