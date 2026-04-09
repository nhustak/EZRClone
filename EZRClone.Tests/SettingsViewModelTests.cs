using EZRClone.Models;
using EZRClone.Services;
using EZRClone.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EZRClone.Tests;

[TestClass]
public class SettingsViewModelTests
{
    [TestMethod]
    public async Task Save_BlankConfigPath_AutoDetectsAndPersistsResolvedPath()
    {
        var tempConfigPath = Path.GetTempFileName();
        var settingsService = new FakeSettingsService();
        var processService = new FakeProcessService
        {
            VersionText = "rclone v1.61.1",
            ConfigPathText = $"Configuration file is stored at:{Environment.NewLine}{tempConfigPath}",
            ExecuteResult = (0, "alpha:\nbeta:\n", string.Empty)
        };

        try
        {
            var viewModel = new SettingsViewModel(settingsService, processService)
            {
                RCloneExePath = typeof(SettingsViewModelTests).Assembly.Location,
                RCloneConfigPath = string.Empty,
                DefaultDownloadPath = "H:\\BU",
                AlwaysGetDirectoryInfo = true,
                DefaultDeleteDryRun = true
            };

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.AreEqual(tempConfigPath, viewModel.RCloneConfigPath);
            Assert.AreEqual(tempConfigPath, settingsService.LastSaved?.RCloneConfigPath);
            StringAssert.Contains(viewModel.ConfigValidationMessage, "Valid");
            StringAssert.Contains(viewModel.ValidationMessage, $"Using config: {tempConfigPath}");
            Assert.AreEqual(tempConfigPath, processService.RCloneConfigPath);
            Assert.IsTrue(viewModel.IsValid);
        }
        finally
        {
            File.Delete(tempConfigPath);
        }
    }

    private sealed class FakeSettingsService : IAppSettingsService
    {
        public AppSettings Current { get; private set; } = new();
        public AppSettings? LastSaved { get; private set; }

        public AppSettings Load() => Current;

        public void Save(AppSettings settings)
        {
            Current = settings;
            LastSaved = settings;
        }
    }

    private sealed class FakeProcessService : IRCloneProcessService
    {
        public string RCloneExePath { get; set; } = string.Empty;
        public string RCloneConfigPath { get; set; } = string.Empty;
        public string VersionText { get; set; } = string.Empty;
        public string ConfigPathText { get; set; } = string.Empty;
        public (int exitCode, string output, string error) ExecuteResult { get; set; }

        public Task<string> RunAsync(string arguments) => Task.FromResult(string.Empty);

        public Task<(int exitCode, string output, string error)> ExecuteAsync(List<string> args) =>
            Task.FromResult(ExecuteResult);

        public Task<string> GetVersionAsync() => Task.FromResult(VersionText);

        public Task<string> GetConfigFilePathAsync() => Task.FromResult(ConfigPathText);

        public Task<RCloneCommandResult> ExecuteDetailedAsync(RCloneCommandRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RCloneCommandResult
            {
                ExitCode = ExecuteResult.exitCode,
                Output = ExecuteResult.output,
                Error = ExecuteResult.error,
                StartedAt = DateTime.UtcNow,
                FinishedAt = DateTime.UtcNow
            });
    }
}
