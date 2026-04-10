using HotCoreUtility.RClone;

namespace EZRClone.Models;

public class AppSettings
{
    public string RCloneExePath { get; set; } = string.Empty;
    public string RCloneConfigPath { get; set; } = string.Empty;
    public string DefaultDownloadPath { get; set; } = string.Empty;
    public int DefaultTransfers { get; set; } = 4;
    public int DefaultCheckers { get; set; } = 8;
    public string DefaultDownloadExtraFlags { get; set; } = string.Empty;
    public RCloneOperationProfileSet OperationProfiles { get; set; } = new();
    public bool AlwaysGetDirectoryInfo { get; set; }
    public bool StartupValidationEnabled { get; set; } = true;
    public bool DefaultDeleteDryRun { get; set; } = true;
}
