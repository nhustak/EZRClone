namespace EZRClone.Models;

public class DownloadRequestOptions
{
    public string DestinationPath { get; set; } = string.Empty;
    public bool PreserveFolderStructure { get; set; }
    public int Transfers { get; set; } = 4;
    public int Checkers { get; set; } = 8;
    public bool DryRun { get; set; }
    public bool CreateLogFile { get; set; }
    public string? LogFilePath { get; set; }
    public string ExtraFlagsText { get; set; } = string.Empty;

    public List<string> ExtraFlags =>
        string.IsNullOrWhiteSpace(ExtraFlagsText)
            ? []
            : ExtraFlagsText.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
}
