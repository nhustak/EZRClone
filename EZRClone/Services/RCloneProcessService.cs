using HotCoreUtility.RClone;
using HotCoreUtility.RClone.Process;

namespace EZRClone.Services;

public class RCloneProcessService : IRCloneProcessService
{
    private readonly IRCloneProcessRunner _processRunner;

    public RCloneProcessService()
        : this(new RCloneProcessRunner())
    {
    }

    public RCloneProcessService(IRCloneProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public string RCloneExePath { get; set; } = string.Empty;

    public async Task<string> RunAsync(string arguments)
    {
        if (string.IsNullOrWhiteSpace(RCloneExePath))
            throw new InvalidOperationException("RClone executable path is not configured.");

        var args = ParseArguments(arguments);
        var result = await _processRunner.ExecuteAsync(ToEnvironment(), new HotCoreUtility.RClone.RCloneCommandRequest
        {
            Arguments = args,
            Operation = args.FirstOrDefault() ?? "rclone"
        });

        if (!result.IsSuccess)
            throw new InvalidOperationException($"rclone error: {string.Join(" ", new[] { result.Error, result.Output }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim()}");

        return result.Output.Trim();
    }

    public Task<string> GetVersionAsync() => _processRunner.GetVersionAsync(ToEnvironment());

    public Task<string> GetConfigFilePathAsync() => _processRunner.GetConfigFilePathAsync(ToEnvironment());

    public async Task<(int exitCode, string output, string error)> ExecuteAsync(List<string> args)
    {
        var result = await ExecuteDetailedAsync(new EZRClone.Models.RCloneCommandRequest
        {
            Arguments = args,
            Operation = args.FirstOrDefault() ?? "rclone"
        });

        return (result.ExitCode, result.Output, result.Error);
    }

    public async Task<EZRClone.Models.RCloneCommandResult> ExecuteDetailedAsync(EZRClone.Models.RCloneCommandRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.ExecuteAsync(ToEnvironment(), new HotCoreUtility.RClone.RCloneCommandRequest
        {
            Arguments = request.Arguments,
            Operation = request.Operation,
            Category = request.Category,
            JobId = request.JobId,
            JobName = request.JobName,
            TimeoutMilliseconds = request.TimeoutMilliseconds,
            OnOutput = request.OnOutput is null
                ? null
                : output => request.OnOutput(new EZRClone.Models.RCloneProcessOutput
                {
                    Text = output.Text,
                    IsError = output.IsError,
                    Timestamp = output.Timestamp
                })
        }, cancellationToken);

        return new EZRClone.Models.RCloneCommandResult
        {
            ExitCode = result.ExitCode,
            Output = result.Output,
            Error = result.Error,
            CommandText = result.CommandText,
            TimedOut = result.TimedOut,
            WasCancelled = result.WasCancelled,
            StartedAt = result.StartedAt,
            FinishedAt = result.FinishedAt
        };
    }

    private RCloneEnvironmentSettings ToEnvironment()
    {
        return new RCloneEnvironmentSettings
        {
            ExePath = RCloneExePath
        };
    }

    private static IReadOnlyList<string> ParseArguments(string arguments)
    {
        var parsed = new List<string>();
        if (string.IsNullOrWhiteSpace(arguments))
            return parsed;

        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var character in arguments)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parsed.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            parsed.Add(current.ToString());
        }

        return parsed;
    }
}
