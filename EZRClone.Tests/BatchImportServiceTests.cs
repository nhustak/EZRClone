using EZRClone.Models;
using EZRClone.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EZRClone.Tests;

[TestClass]
public class BatchImportServiceTests
{
    [TestMethod]
    public void ImportFromFile_ParsesQuotedPathsAndFlags()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile,
                "rclone copy \"C:\\Source Folder\" Nassau:archive --transfers 8 --log-file=\"C:\\Logs\\copy.log\" --include \"*.bak\" -vv");

            var service = new BatchImportService();
            var result = service.ImportFromFile(tempFile);

            Assert.AreEqual(1, result.Jobs.Count);
            var job = result.Jobs[0];
            Assert.AreEqual(RCloneOperation.Copy, job.Operation);
            Assert.AreEqual("C:\\Source Folder", job.SourcePath);
            Assert.AreEqual("archive", job.DestinationPath);
            Assert.AreEqual("Nassau", job.DestinationRemoteName);
            Assert.AreEqual(8, job.Transfers);
            Assert.AreEqual("C:\\Logs\\copy.log", job.LogFilePath);
            CollectionAssert.Contains(job.IncludePatterns, "*.bak");
            Assert.AreEqual(RCloneVerbosity.VeryVerbose, job.Verbosity);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
