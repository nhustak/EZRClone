namespace EZRClone.Models;

public class AppLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public AppLogSeverity Severity { get; set; } = AppLogSeverity.Info;
    public string Category { get; set; } = "General";
    public string Operation { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? CommandText { get; set; }
    public string? OutputSnippet { get; set; }
    public string? ErrorSnippet { get; set; }
    public int? ExitCode { get; set; }
    public string? JobId { get; set; }
    public string? JobName { get; set; }
    public bool IsHistory { get; set; }

    public string DisplayTimestamp => Timestamp.ToString("g");

    public string SummaryText
    {
        get
        {
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(Operation))
                details.Add(Operation);
            if (ExitCode is int exitCode)
                details.Add($"exit {exitCode}");
            if (!string.IsNullOrWhiteSpace(JobName))
                details.Add(JobName!);

            return details.Count == 0 ? Message : $"{Message} ({string.Join(" • ", details)})";
        }
    }

    public string DetailsText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(CommandText))
                parts.Add($"Command: {CommandText}");
            if (!string.IsNullOrWhiteSpace(OutputSnippet))
                parts.Add($"Output: {OutputSnippet}");
            if (!string.IsNullOrWhiteSpace(ErrorSnippet))
                parts.Add($"Error: {ErrorSnippet}");

            return string.Join(Environment.NewLine, parts);
        }
    }
}

public enum AppLogSeverity
{
    Info,
    Warning,
    Error
}
