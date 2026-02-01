namespace EZRClone.Services;

public interface IRCloneProcessService
{
    string RCloneExePath { get; set; }
    Task<string> RunAsync(string arguments);
    Task<(int exitCode, string output, string error)> ExecuteAsync(List<string> args);
    Task<string> GetVersionAsync();
    Task<string> GetConfigFilePathAsync();
}
