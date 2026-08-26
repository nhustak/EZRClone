using EZRClone.Models;
using EZRClone.Services;
using EZRClone.ViewModels;
using HotCoreUtility.RClone;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AppRCloneCommandRequest = EZRClone.Models.RCloneCommandRequest;
using AppRCloneCommandResult = EZRClone.Models.RCloneCommandResult;

namespace EZRClone.Tests;

[TestClass]
public class JobsViewModelTests
{
    [TestMethod]
    public async Task Clone_PersistsCopyWithNewIdUniqueNameAndClearedRunState()
    {
        var storage = new FakeJobStorage();
        var viewModel = CreateViewModel(storage);
        var original = new RCloneJob
        {
            Name = "Daily Sync",
            Operation = RCloneOperation.Sync,
            SourcePath = "src",
            SourceIsRemote = true,
            SourceRemoteName = "EOM",
            DestinationPath = "dst",
            DestinationIsRemote = false,
            Transfers = 8,
            CreateLogFile = true,
            LogFilePath = @"C:\old.log",
            IncludePatterns = ["*.zip"],
            ExtraFlags = ["--fast-list"],
            LastRun = DateTime.Now,
            LastStatus = RCloneJobStatus.Success,
            LastError = "none"
        };
        viewModel.Jobs.Add(original);
        viewModel.SelectedJob = original;

        await viewModel.CloneCommand.ExecuteAsync(null);

        Assert.AreEqual(2, viewModel.Jobs.Count);
        Assert.AreEqual(2, storage.Saved.Count);
        var clone = viewModel.Jobs.Single(job => job.Id != original.Id);
        Assert.AreEqual("Daily Sync Clone", clone.Name);
        Assert.AreNotEqual(original.Id, clone.Id);
        Assert.AreEqual(RCloneOperation.Sync, clone.Operation);
        Assert.AreEqual("src", clone.SourcePath);
        Assert.AreEqual("EOM", clone.SourceRemoteName);
        Assert.AreEqual(8, clone.Transfers);
        CollectionAssert.AreEqual(new[] { "*.zip" }, clone.IncludePatterns);
        CollectionAssert.AreEqual(new[] { "--fast-list" }, clone.ExtraFlags);
        Assert.AreEqual(RCloneJobStatus.NotRun, clone.LastStatus);
        Assert.IsNull(clone.LastRun);
        Assert.IsNull(clone.LastError);
        Assert.AreNotEqual(original.LogFilePath, clone.LogFilePath);
        StringAssert.Contains(clone.LogFilePath, "Daily Sync Clone");
        Assert.AreEqual(clone, viewModel.SelectedJob);
        Assert.AreEqual("Cloned job 'Daily Sync Clone'.", viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task Clone_SecondCloneGetsNumberedName()
    {
        var viewModel = CreateViewModel(new FakeJobStorage());
        var original = new RCloneJob { Name = "Backup", SourcePath = "src" };
        viewModel.Jobs.Add(original);
        viewModel.SelectedJob = original;

        await viewModel.CloneCommand.ExecuteAsync(null);
        viewModel.SelectedJob = original;
        await viewModel.CloneCommand.ExecuteAsync(null);

        CollectionAssert.AreEquivalent(
            new[] { "Backup", "Backup Clone", "Backup Clone 2" },
            viewModel.Jobs.Select(job => job.Name).ToArray());
    }

    [TestMethod]
    public void Clone_DisabledWhenNoJobIsSelected()
    {
        var viewModel = CreateViewModel(new FakeJobStorage());

        Assert.IsFalse(viewModel.CloneCommand.CanExecute(null));
    }

    private static JobsViewModel CreateViewModel(FakeJobStorage storage)
    {
        return new JobsViewModel(
            storage,
            new FakeConfigService(),
            new FakeProcessService(),
            new FakeSettingsService(),
            new FakeBatchImportService(),
            new FakeLogService());
    }

    private sealed class FakeJobStorage : IJobStorageService
    {
        public List<RCloneJob> Saved { get; private set; } = [];

        public Task<List<RCloneJob>> LoadJobsAsync() => Task.FromResult(new List<RCloneJob>());

        public Task SaveJobsAsync(List<RCloneJob> jobs)
        {
            Saved = jobs.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConfigService : IRCloneConfigService
    {
        public Task<List<RCloneRemote>> ReadConfigAsync(string configPath) =>
            Task.FromResult(new List<RCloneRemote>());

        public Task WriteConfigAsync(string configPath, List<RCloneRemote> remotes) => Task.CompletedTask;
    }

    private sealed class FakeProcessService : IRCloneProcessService
    {
        public string RCloneExePath { get; set; } = string.Empty;
        public string RCloneConfigPath { get; set; } = string.Empty;
        public RCloneOperationProfileSet OperationProfiles { get; set; } = new();

        public Task<string> RunAsync(string arguments) => Task.FromResult(string.Empty);

        public Task<(int exitCode, string output, string error)> ExecuteAsync(List<string> args) =>
            Task.FromResult((0, string.Empty, string.Empty));

        public Task<string> GetVersionAsync() => Task.FromResult(string.Empty);

        public Task<string> GetConfigFilePathAsync() => Task.FromResult(string.Empty);

        public Task<AppRCloneCommandResult> ExecuteDetailedAsync(AppRCloneCommandRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppRCloneCommandResult
            {
                StartedAt = DateTime.UtcNow,
                FinishedAt = DateTime.UtcNow
            });
    }

    private sealed class FakeSettingsService : IAppSettingsService
    {
        public AppSettings Load() => new();

        public void Save(AppSettings settings)
        {
        }
    }

    private sealed class FakeBatchImportService : IBatchImportService
    {
        public BatchImportResult ImportFromFile(string filePath) => new();
    }

    private sealed class FakeLogService : IAppLogService
    {
        public event EventHandler<AppLogEntry>? SessionEntryAdded;
        public event EventHandler? HistoryChanged;

        public IReadOnlyList<AppLogEntry> GetSessionEntries() => [];
        public IReadOnlyList<AppLogEntry> GetRunHistory() => [];
        public void AddSessionEntry(AppLogEntry entry) => SessionEntryAdded?.Invoke(this, entry);
        public void ClearSession() => HistoryChanged?.Invoke(this, EventArgs.Empty);
        public Task AddRunHistoryEntryAsync(AppLogEntry entry) => Task.CompletedTask;
        public Task LoadRunHistoryAsync() => Task.CompletedTask;
    }
}
