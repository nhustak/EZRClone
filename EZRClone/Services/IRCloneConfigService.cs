using EZRClone.Models;

namespace EZRClone.Services;

public interface IRCloneConfigService
{
    Task<List<RCloneRemote>> ReadConfigAsync(string configPath);
    Task WriteConfigAsync(string configPath, List<RCloneRemote> remotes);
}
