using System.Diagnostics;
using System.IO;
using System.Text;
using EZRClone.Models;

namespace EZRClone.Services;

public class RCloneProcessService : IRCloneProcessService
{
    public string RCloneExePath { get; set; } = string.Empty;

    public async Task<string> RunAsync(string arguments)
    {
        if (string.IsNullOrWhiteSpace(RCloneExePath))
            throw new InvalidOperationException("RClone executable path is not configured.");

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = RCloneExePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException($"rclone error: {error.Trim()}");

        return output.Trim();
    }

    public Task<string> GetVersionAsync() => RunAsync("version");

    public Task<string> GetConfigFilePathAsync() => RunAsync("config file");

    public async Task<(int exitCode, string output, string error)> ExecuteAsync(List<string> args)
    {
        var result = await ExecuteDetailedAsync(new RCloneCommandRequest
        {
            Arguments = args,
            Operation = args.FirstOrDefault() ?? "rclone"
        });

        return (result.ExitCode, result.Output, result.Error);
    }

    public async Task<RCloneCommandResult> ExecuteDetailedAsync(RCloneCommandRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RCloneExePath))
            throw new InvalidOperationException("RClone executable path is not configured.");

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = RCloneExePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in request.Arguments)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        var commandText = BuildDisplayCommand(request.Arguments);
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var startedAt = DateTime.Now;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (outputBuilder)
            {
                outputBuilder.AppendLine(e.Data);
            }

            request.OnOutput?.Invoke(new RCloneProcessOutput
            {
                Text = e.Data,
                IsError = false,
                Timestamp = DateTime.Now
            });
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (errorBuilder)
            {
                errorBuilder.AppendLine(e.Data);
            }

            request.OnOutput?.Invoke(new RCloneProcessOutput
            {
                Text = e.Data,
                IsError = true,
                Timestamp = DateTime.Now
            });
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = request.TimeoutMilliseconds is int timeout
            ? new CancellationTokenSource(timeout)
            : null;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts?.Token ?? CancellationToken.None);

        var timedOut = false;
        var wasCancelled = false;

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
        {
            timedOut = true;
            TryTerminate(process);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            TryTerminate(process);
        }

        if (!process.HasExited)
        {
            await process.WaitForExitAsync();
        }

        return new RCloneCommandResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString().Trim(),
            Error = errorBuilder.ToString().Trim(),
            CommandText = commandText,
            TimedOut = timedOut,
            WasCancelled = wasCancelled,
            StartedAt = startedAt,
            FinishedAt = DateTime.Now
        };
    }

    private string BuildDisplayCommand(IReadOnlyList<string> arguments)
    {
        var displayArgs = arguments.Select(arg =>
            arg.Contains(' ') ? $"\"{arg}\"" : arg);
        return $"{Path.GetFileName(RCloneExePath)} {string.Join(" ", displayArgs)}";
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
            // Best-effort termination only.
        }
    }
}
