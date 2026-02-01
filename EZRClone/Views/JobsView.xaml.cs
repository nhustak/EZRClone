using System.Windows;
using System.Windows.Controls;
using EZRClone.Models;
using Microsoft.Win32;

namespace EZRClone.Views;

public partial class JobsView : UserControl
{
    public JobsView()
    {
        InitializeComponent();
    }

    private void OnBrowseSourcePath(object sender, RoutedEventArgs e)
    {
        var path = BrowseForFolder("Select Source Folder");
        if (path != null && GetEditingJob() is { } job)
        {
            job.SourcePath = path;
        }
    }

    private void OnBrowseDestinationPath(object sender, RoutedEventArgs e)
    {
        var path = BrowseForFolder("Select Destination Folder");
        if (path != null && GetEditingJob() is { } job)
        {
            job.DestinationPath = path;
        }
    }

    private void OnBrowseLogFilePath(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Select Log File Location",
            Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".log"
        };

        if (GetEditingJob() is { } job && !string.IsNullOrEmpty(job.LogFilePath))
        {
            try
            {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(job.LogFilePath) ?? "";
                dialog.FileName = System.IO.Path.GetFileName(job.LogFilePath);
            }
            catch { }
        }

        if (dialog.ShowDialog() == true && GetEditingJob() is { } editJob)
        {
            editJob.LogFilePath = dialog.FileName;
        }
    }

    private static string? BrowseForFolder(string description)
    {
        var dialog = new OpenFolderDialog
        {
            Title = description
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private void OnImportBatch(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Batch File",
            Filter = "Batch files (*.bat;*.cmd)|*.bat;*.cmd|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true && DataContext is ViewModels.JobsViewModel vm)
        {
            vm.ImportFromBatchFileCommand.Execute(dialog.FileName);
        }
    }

    private RCloneJob? GetEditingJob()
    {
        return (DataContext as ViewModels.JobsViewModel)?.EditingJob;
    }
}
