using System.IO;
using EZRClone.Models;
using HotCoreUtility.RClone;
using HotCoreUtility.RClone.Config;

namespace EZRClone.Services;

public class RCloneConfigService : IRCloneConfigService
{
    private readonly IRCloneConfigManager _configManager;

    public RCloneConfigService()
        : this(new RCloneConfigManager())
    {
    }

    public RCloneConfigService(IRCloneConfigManager configManager)
    {
        _configManager = configManager;
    }

    public List<RCloneRemote> ReadConfig(string configPath)
    {
        if (!File.Exists(configPath))
            return [];

        return _configManager.ReadAllAsync(configPath).GetAwaiter().GetResult()
            .Select(ToRemoteModel)
            .ToList();
    }

    public void WriteConfig(string configPath, List<RCloneRemote> remotes)
    {
        _configManager.WriteFullConfigAsync(configPath, remotes.Select(ToRemoteDefinition).ToList()).GetAwaiter().GetResult();
    }

    private static RCloneRemoteDefinition ToRemoteDefinition(RCloneRemote remote)
    {
        return new RCloneRemoteDefinition
        {
            Name = remote.Name,
            Type = remote.Type,
            Properties = new Dictionary<string, string>(remote.Properties, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static RCloneRemote ToRemoteModel(RCloneRemoteDefinition remote)
    {
        return new RCloneRemote
        {
            Name = remote.Name,
            Type = remote.Type,
            Properties = new Dictionary<string, string>(remote.Properties, StringComparer.OrdinalIgnoreCase)
        };
    }
}
