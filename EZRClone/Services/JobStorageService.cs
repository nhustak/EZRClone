using System.IO;
using System.Text.Json;
using EZRClone.Models;

namespace EZRClone.Services;

public class JobStorageService : IJobStorageService
{
    private readonly string _jobsFilePath;

    public JobStorageService()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EZRClone"
        );
        Directory.CreateDirectory(appDataFolder);
        _jobsFilePath = Path.Combine(appDataFolder, "jobs.json");
    }

    public async Task<List<RCloneJob>> LoadJobsAsync()
    {
        if (!File.Exists(_jobsFilePath))
        {
            return new List<RCloneJob>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_jobsFilePath);
            return JsonSerializer.Deserialize<List<RCloneJob>>(json) ?? new List<RCloneJob>();
        }
        catch
        {
            return new List<RCloneJob>();
        }
    }

    public async Task SaveJobsAsync(List<RCloneJob> jobs)
    {
        var json = JsonSerializer.Serialize(jobs, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_jobsFilePath, json);
    }
}
