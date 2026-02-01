using EZRClone.Models;

namespace EZRClone.Services;

public interface IJobStorageService
{
    Task<List<RCloneJob>> LoadJobsAsync();
    Task SaveJobsAsync(List<RCloneJob> jobs);
}
