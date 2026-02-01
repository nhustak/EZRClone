using EZRClone.Models;

namespace EZRClone.Services;

public interface IRCloneConfigService
{
    List<RCloneRemote> ReadConfig(string configPath);
    void WriteConfig(string configPath, List<RCloneRemote> remotes);
}
