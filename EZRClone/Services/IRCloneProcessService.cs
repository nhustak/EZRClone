namespace EZRClone.Services;

using HotCoreUtility.RClone;
using EZRCloneCommandRequest = EZRClone.Models.RCloneCommandRequest;
using EZRCloneCommandResult = EZRClone.Models.RCloneCommandResult;

public interface IRCloneProcessService
{
    string RCloneExePath { get; set; }
    string RCloneConfigPath { get; set; }
    RCloneOperationProfileSet OperationProfiles { get; set; }
    Task<string> RunAsync(string arguments);
    Task<(int exitCode, string output, string error)> ExecuteAsync(List<string> args);
    Task<string> GetVersionAsync();
    Task<string> GetConfigFilePathAsync();
    Task<EZRCloneCommandResult> ExecuteDetailedAsync(EZRCloneCommandRequest request, CancellationToken cancellationToken = default);
}
