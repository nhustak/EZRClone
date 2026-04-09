namespace EZRClone.Models;

public class RCloneCommandRequest
{
    public required IReadOnlyList<string> Arguments { get; init; }
    public string Operation { get; init; } = string.Empty;
    public string Category { get; init; } = "RClone";
    public string? JobId { get; init; }
    public string? JobName { get; init; }
    public int? TimeoutMilliseconds { get; init; }
    public Action<RCloneProcessOutput>? OnOutput { get; init; }
}

public class RCloneCommandResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public string CommandText { get; init; } = string.Empty;
    public bool TimedOut { get; init; }
    public bool WasCancelled { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime FinishedAt { get; init; }
    public TimeSpan Duration => FinishedAt - StartedAt;
    public bool IsSuccess => ExitCode == 0 && !TimedOut && !WasCancelled;
}

public class RCloneProcessOutput
{
    public required string Text { get; init; }
    public required bool IsError { get; init; }
    public required DateTime Timestamp { get; init; }
}
