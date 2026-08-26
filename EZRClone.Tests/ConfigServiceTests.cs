using EZRClone.Models;
using EZRClone.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EZRClone.Tests;

[TestClass]
public class ConfigServiceTests
{
    [TestMethod]
    public async Task ReadConfig_ParsesRemotesAndSkipsComments()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                # comment
                [alpha]
                type = s3
                region = us-east-1

                [beta]
                type = drive
                team_drive = shared
                """);

            var service = new RCloneConfigService();
            var remotes = await service.ReadConfigAsync(tempFile);

            Assert.AreEqual(2, remotes.Count);
            Assert.AreEqual("alpha", remotes[0].Name);
            Assert.AreEqual("s3", remotes[0].Type);
            Assert.AreEqual("us-east-1", remotes[0].Properties["region"]);
            Assert.AreEqual("beta", remotes[1].Name);
            Assert.AreEqual("drive", remotes[1].Type);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task WriteConfig_WritesExpectedSections()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new RCloneConfigService();
            await service.WriteConfigAsync(tempFile, new List<RCloneRemote>
            {
                new()
                {
                    Name = "alpha",
                    Type = "s3",
                    Properties = new Dictionary<string, string> { ["region"] = "us-east-1" }
                }
            });

            var content = File.ReadAllText(tempFile);
            StringAssert.Contains(content, "[alpha]");
            StringAssert.Contains(content, "type = s3");
            StringAssert.Contains(content, "region = us-east-1");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
