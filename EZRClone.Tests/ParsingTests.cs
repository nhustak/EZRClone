using EZRClone.Models;
using EZRClone.ViewModels;
using HotCoreUtility.RClone;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EZRClone.Tests;

[TestClass]
public class ParsingTests
{
    [TestMethod]
    public void BuildRCloneArgs_IncludesDryRunAndRemotePaths()
    {
        var job = new RCloneJob
        {
            Operation = RCloneOperation.Sync,
            SourceIsRemote = true,
            SourceRemoteName = "EOM",
            SourcePath = "source",
            DestinationPath = "C:\\Target",
            Transfers = 6,
            DryRun = true,
            IncludePatterns = ["*.zip"]
        };

        var args = JobsViewModel.BuildRCloneArgs(job, effectiveDryRun: true);

        Assert.AreEqual("sync", args[0]);
        CollectionAssert.Contains(args, "EOM:source");
        CollectionAssert.Contains(args, "C:\\Target");
        CollectionAssert.Contains(args, "--include");
        CollectionAssert.Contains(args, "*.zip");
    }

    [TestMethod]
    public void OperationOptions_Merge_PrefersOverridesAndAppendsExtraFlags()
    {
        var defaults = new RCloneOperationOptions
        {
            Transfers = 4,
            Checkers = 8,
            ExtraFlagsText = "--ignore-existing"
        };
        var overrides = new RCloneOperationOptions
        {
            Transfers = 12,
            ExtraFlagsText = "--bwlimit 50M"
        };

        var merged = RCloneOperationOptions.Merge(defaults, overrides);

        Assert.AreEqual(12, merged.Transfers);
        Assert.AreEqual(8, merged.Checkers);
        CollectionAssert.AreEqual(
            new[] { "--ignore-existing", "--bwlimit", "50M" },
            merged.GetExtraFlags().ToArray());
    }

    [TestMethod]
    public void ParseSearchOutput_ParsesDirectoriesAndFiles()
    {
        var items = SearchViewModel.ParseSearchOutput("""
            docs/readme.txt	128	2026-04-09 10:15:00
            folder/	-1	2026-04-09 10:16:00
            """);

        Assert.AreEqual(2, items.Count);
        Assert.IsFalse(items[0].IsDirectory);
        Assert.AreEqual("readme.txt", items[0].Name);
        Assert.IsTrue(items[1].IsDirectory);
    }

    [TestMethod]
    public void ParseLsfOutput_SortsDirectoriesBeforeFiles()
    {
        var items = BrowseViewModel.ParseLsfOutput("""
            z-last.txt	45	2026-04-09 10:16:00	inode/file
            alpha/	-1	2026-04-09 10:15:00	inode/directory
            """, "");

        Assert.AreEqual(2, items.Count);
        Assert.IsTrue(items[0].IsDirectory);
        Assert.AreEqual("alpha", items[0].Name);
        Assert.AreEqual("z-last.txt", items[1].Name);
    }
}
