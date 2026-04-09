using EZRClone.Models;
using EZRClone.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EZRClone.Tests;

[TestClass]
public class AppLogServiceTests
{
    [TestMethod]
    public async Task AddRunHistoryEntryAsync_PersistsAndReloads()
    {
        var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
        var tempAppData = Path.Combine(Path.GetTempPath(), "EZRCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempAppData);
        Environment.SetEnvironmentVariable("APPDATA", tempAppData);

        try
        {
            var service = new AppLogService();
            var entry = new AppLogEntry
            {
                Category = "Job Run",
                Message = "Completed",
                JobName = "Nightly Sync",
                ExitCode = 0
            };

            await service.AddRunHistoryEntryAsync(entry);

            var secondService = new AppLogService();
            await secondService.LoadRunHistoryAsync();

            Assert.AreEqual(1, secondService.GetRunHistory().Count);
            var loaded = secondService.GetRunHistory()[0];
            Assert.AreEqual("Completed", loaded.Message);
            Assert.AreEqual("Nightly Sync", loaded.JobName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPDATA", originalAppData);
            if (Directory.Exists(tempAppData))
                Directory.Delete(tempAppData, true);
        }
    }
}
