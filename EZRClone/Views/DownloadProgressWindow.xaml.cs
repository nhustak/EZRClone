using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace EZRClone.Views;

public partial class DownloadProgressWindow : Window
{
    private static readonly Regex SpeedEtaRegex = new(
        @"(?<speed>[^,]+/s)(?:,\s*ETA\s*(?<eta>.+))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly StringBuilder _outputBuffer = new();

    public DownloadProgressWindow(int totalItems, string destinationPath)
    {
        InitializeComponent();
        OverallStatusText.Text = $"Preparing download of {totalItems} item{(totalItems != 1 ? "s" : "")}...";
        CurrentItemText.Text = "Waiting for rclone to start.";
        TargetText.Text = $"Target folder: {destinationPath}";
        SpeedEtaText.Text = "Speed and ETA will appear when rclone starts reporting progress.";
        ProgressLineText.Text = string.Empty;
        CompletionText.Text = "Download running...";
    }

    public void SetCurrentItem(int index, int totalItems, string itemName, string targetPath)
    {
        Dispatcher.Invoke(() =>
        {
            OverallStatusText.Text = $"Downloading item {index} of {totalItems}";
            CurrentItemText.Text = itemName;
            TargetText.Text = $"Target: {targetPath}";
        });
    }

    public void ReportOutput(string text, bool isError)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Dispatcher.Invoke(() =>
        {
            if (_outputBuffer.Length > 12000)
            {
                _outputBuffer.Remove(0, 4000);
            }

            var prefix = isError ? "[err] " : string.Empty;
            _outputBuffer.AppendLine(prefix + text);
            OutputTextBox.Text = _outputBuffer.ToString();
            OutputTextBox.ScrollToEnd();

            ProgressLineText.Text = text;

            var match = SpeedEtaRegex.Match(text);
            if (match.Success)
            {
                var speed = match.Groups["speed"].Value.Trim();
                var eta = match.Groups["eta"].Success ? match.Groups["eta"].Value.Trim() : "calculating";
                SpeedEtaText.Text = $"Speed: {speed}   ETA: {eta}";
            }
            else if (text.Contains("ETA", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("/s", StringComparison.OrdinalIgnoreCase))
            {
                SpeedEtaText.Text = text;
            }
        });
    }

    public void Complete(string summary, bool hasErrors)
    {
        Dispatcher.Invoke(() =>
        {
            CompletionText.Text = summary;
            SpeedEtaText.Text = hasErrors
                ? "Download finished with errors."
                : "Download completed successfully.";
            CloseButton.IsEnabled = true;
            Activate();
        });
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
