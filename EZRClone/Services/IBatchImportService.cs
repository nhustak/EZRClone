using EZRClone.Models;

namespace EZRClone.Services;

public class BatchImportResult
{
    public List<RCloneJob> Jobs { get; set; } = new();
    public List<string> SkippedLines { get; set; } = new();
}

public interface IBatchImportService
{
    BatchImportResult ImportFromFile(string filePath);
}
