namespace EZRClone.Models;

public class RCloneJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public RCloneOperation Operation { get; set; } = RCloneOperation.Copy;
    public string SourcePath { get; set; } = string.Empty;
    public bool SourceIsRemote { get; set; }
    public string? SourceRemoteName { get; set; }
    public string DestinationPath { get; set; } = string.Empty;
    public bool DestinationIsRemote { get; set; }
    public string? DestinationRemoteName { get; set; }
    
    // Common options
    public int Transfers { get; set; } = 4;
    public bool CreateLogFile { get; set; } = true;
    public string? LogFilePath { get; set; }
    public RCloneVerbosity Verbosity { get; set; } = RCloneVerbosity.Normal;
    
    // Filtering options
    public List<string> IncludePatterns { get; set; } = new();
    public List<string> ExcludePatterns { get; set; } = new();

    // Delete operation options
    public string? MinAge { get; set; }
    public List<string> ExtraFlags { get; set; } = new();

    /// <summary>Space-separated extra flags for UI binding.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ExtraFlagsText
    {
        get => string.Join(" ", ExtraFlags);
        set => ExtraFlags = string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
    
    // Scheduling (for future use)
    public bool IsScheduled { get; set; }
    public string? ScheduleCron { get; set; }
    
    // Status
    public DateTime? LastRun { get; set; }
    public RCloneJobStatus LastStatus { get; set; } = RCloneJobStatus.NotRun;
    public string? LastError { get; set; }
}

public enum RCloneOperation
{
    Copy,    // Copy files from source to dest, skipping identical files
    Sync,    // Make destination identical to source (one way)
    Move,    // Move files from source to dest
    Delete   // Delete files from remote
}

public enum RCloneVerbosity
{
    Quiet,   // -q
    Normal,  // default
    Verbose, // -v
    VeryVerbose // -vv
}

public enum RCloneJobStatus
{
    NotRun,
    Running,
    Success,
    Failed,
    Cancelled
}
