using System.IO;
using System.Windows;
using EZRClone.Models;
using Microsoft.Win32;

namespace EZRClone.Views;

public partial class DownloadOptionsWindow : Window
{
    private readonly IReadOnlyList<RemoteItem> _items;

    public DownloadRequestOptions? Result { get; private set; }

    public DownloadOptionsWindow(
        string remoteName,
        IReadOnlyList<RemoteItem> items,
        string initialDestinationPath,
        bool defaultPreserveFolderStructure,
        int defaultTransfers,
        int defaultCheckers,
        string defaultExtraFlags)
    {
        InitializeComponent();

        _items = items;
        DestinationPathTextBox.Text = initialDestinationPath;
        PreserveStructureCheckBox.IsChecked = defaultPreserveFolderStructure;
        TransfersTextBox.Text = Math.Max(1, defaultTransfers).ToString();
        CheckersTextBox.Text = Math.Max(1, defaultCheckers).ToString();
        ExtraFlagsTextBox.Text = defaultExtraFlags;
        CreateLogCheckBox.IsChecked = false;
        ToggleLogControls();

        SummaryText.Text = items.Count == 1
            ? $"Download 1 item from remote '{remoteName}'."
            : $"Download {items.Count} items from remote '{remoteName}'.";

        var fileCount = items.Count(item => !item.IsDirectory);
        var directoryCount = items.Count - fileCount;
        var knownBytes = items.Where(item => !item.IsDirectory && item.Size > 0).Sum(item => item.Size);

        var preview = string.Join(", ", items.Take(3).Select(item => item.Path));
        if (items.Count > 3)
            preview += ", ...";

        var breakdown = $"{fileCount} file{(fileCount != 1 ? "s" : "")}";
        if (directoryCount > 0)
            breakdown += $", {directoryCount} director{(directoryCount != 1 ? "ies" : "y")}";

        if (knownBytes > 0)
            breakdown += $" • known file size {RemoteItem.FormatSize(knownBytes)}";

        PreviewText.Text = $"{breakdown}\n{preview}";
    }

    private void OnBrowseDestination(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select download folder",
            InitialDirectory = string.IsNullOrWhiteSpace(DestinationPathTextBox.Text)
                ? null
                : DestinationPathTextBox.Text
        };

        if (dialog.ShowDialog() == true)
            DestinationPathTextBox.Text = dialog.FolderName;
    }

    private void OnBrowseLogFile(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Select log file location",
            Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".log"
        };

        if (dialog.ShowDialog() == true)
            LogFilePathTextBox.Text = dialog.FileName;
    }

    private void OnCreateLogChecked(object sender, RoutedEventArgs e)
    {
        ToggleLogControls();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;

        var destinationPath = DestinationPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            ValidationText.Text = "Choose a target folder.";
            return;
        }

        if (!int.TryParse(TransfersTextBox.Text.Trim(), out var transfers) || transfers <= 0)
        {
            ValidationText.Text = "Transfers must be a positive whole number.";
            return;
        }

        if (!int.TryParse(CheckersTextBox.Text.Trim(), out var checkers) || checkers <= 0)
        {
            ValidationText.Text = "Checkers must be a positive whole number.";
            return;
        }

        var preserveStructure = PreserveStructureCheckBox.IsChecked == true;
        if (!preserveStructure)
        {
            var duplicateNames = _items
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateNames.Count > 0)
            {
                ValidationText.Text = "Selected items contain duplicate names. Enable folder structure preservation or reduce the selection.";
                return;
            }
        }

        string? logFilePath = null;
        if (CreateLogCheckBox.IsChecked == true)
        {
            logFilePath = LogFilePathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                ValidationText.Text = "Choose a log file path or turn off log file creation.";
                return;
            }
        }

        Result = new DownloadRequestOptions
        {
            DestinationPath = destinationPath,
            PreserveFolderStructure = preserveStructure,
            Transfers = transfers,
            Checkers = checkers,
            DryRun = DryRunCheckBox.IsChecked == true,
            CreateLogFile = CreateLogCheckBox.IsChecked == true,
            LogFilePath = logFilePath,
            ExtraFlagsText = ExtraFlagsTextBox.Text.Trim()
        };

        DialogResult = true;
        Close();
    }

    private void ToggleLogControls()
    {
        var enabled = CreateLogCheckBox.IsChecked == true;
        LogFilePathTextBox.IsEnabled = enabled;
        BrowseLogButton.IsEnabled = enabled;

        if (!enabled)
            LogFilePathTextBox.Text = string.Empty;
    }
}
