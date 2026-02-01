namespace EZRClone.Models;

public class RCloneRemote
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
}
