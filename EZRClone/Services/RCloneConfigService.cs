using System.IO;
using EZRClone.Models;

namespace EZRClone.Services;

public class RCloneConfigService : IRCloneConfigService
{
    public List<RCloneRemote> ReadConfig(string configPath)
    {
        var remotes = new List<RCloneRemote>();

        if (!File.Exists(configPath))
            return remotes;

        var lines = File.ReadAllLines(configPath);
        RCloneRemote? current = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            // Section header: [remoteName]
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = new RCloneRemote
                {
                    Name = line[1..^1].Trim()
                };
                remotes.Add(current);
                continue;
            }

            // Key = value pair
            if (current is not null)
            {
                var eqIndex = line.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = line[..eqIndex].Trim();
                    var value = line[(eqIndex + 1)..].Trim();

                    if (key == "type")
                        current.Type = value;
                    else
                        current.Properties[key] = value;
                }
            }
        }

        return remotes;
    }

    public void WriteConfig(string configPath, List<RCloneRemote> remotes)
    {
        using var writer = new StreamWriter(configPath, false);

        for (var i = 0; i < remotes.Count; i++)
        {
            var remote = remotes[i];

            if (i > 0)
                writer.WriteLine();

            writer.WriteLine($"[{remote.Name}]");
            writer.WriteLine($"type = {remote.Type}");

            foreach (var kvp in remote.Properties)
            {
                writer.WriteLine($"{kvp.Key} = {kvp.Value}");
            }
        }
    }
}
