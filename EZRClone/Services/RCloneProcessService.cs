using System.Diagnostics;

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
        if (string.IsNullOrWhiteSpace(RCloneExePath))
            throw new InvalidOperationException("RClone executable path is not configured.");

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = RCloneExePath,
            ArgumentList = { },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output.Trim(), error.Trim());
    }
}
