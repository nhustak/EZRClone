namespace EZRClone.Services;

using EZRClone.Models;

public interface IRCloneProcessService
{
    string RCloneExePath { get; set; }
    string RCloneConfigPath { get; set; }
    Task<string> RunAsync(string arguments);
    Task<(int exitCode, string output, string error)> ExecuteAsync(List<string> args);
    Task<string> GetVersionAsync();
    Task<string> GetConfigFilePathAsync();
    Task<RCloneCommandResult> ExecuteDetailedAsync(RCloneCommandRequest request, CancellationToken cancellationToken = default);
}
