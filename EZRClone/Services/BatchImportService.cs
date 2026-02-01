using System.IO;
using System.Text.RegularExpressions;
using EZRClone.Models;

namespace EZRClone.Services;

public partial class BatchImportService : IBatchImportService
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "copy", "sync", "move", "delete"
    };

    private static readonly HashSet<string> FlagsWithValue = new(StringComparer.OrdinalIgnoreCase)
    {
        "--transfers", "--log-file", "--min-age", "--include", "--exclude", "--config"
    };

    public BatchImportResult ImportFromFile(string filePath)
    {
        var result = new BatchImportResult();
        var lines = File.ReadAllLines(filePath);
        var baseName = Path.GetFileNameWithoutExtension(filePath);
        var jobIndex = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var parsed = TryParseRCloneLine(trimmed);
            if (parsed == null)
                continue;

            jobIndex++;
            parsed.Name = jobIndex == 1 ? baseName : $"{baseName}-{jobIndex}";
            result.Jobs.Add(parsed);
        }

        return result;
    }

    private static RCloneJob? TryParseRCloneLine(string line)
    {
        var tokens = Tokenize(line);
        if (tokens.Count < 2)
            return null;

        // Find the rclone executable token and the operation that follows it
        int opIndex = -1;
        string? operation = null;

        for (int i = 0; i < tokens.Count - 1; i++)
        {
            var token = tokens[i];
            var name = Path.GetFileNameWithoutExtension(token);
            if (name.Equals("rclone", StringComparison.OrdinalIgnoreCase))
            {
                var nextToken = tokens[i + 1];
                if (SupportedOperations.Contains(nextToken))
                {
                    opIndex = i + 1;
                    operation = nextToken.ToLower();
                    break;
                }
            }
        }

        if (opIndex < 0 || operation == null)
            return null;

        // Parse flags and positional args starting after the operation
        var positionalArgs = new List<string>();
        int transfers = 4;
        string? logFile = null;
        string? minAge = null;
        var verbosity = RCloneVerbosity.Normal;
        var includePatterns = new List<string>();
        var excludePatterns = new List<string>();
        var extraFlags = new List<string>();

        int i2 = opIndex + 1;
        while (i2 < tokens.Count)
        {
            var tok = tokens[i2];

            if (tok == "-v" || tok.Equals("--verbose", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = RCloneVerbosity.Verbose;
                i2++;
            }
            else if (tok == "-vv")
            {
                verbosity = RCloneVerbosity.VeryVerbose;
                i2++;
            }
            else if (tok == "-q" || tok.Equals("--quiet", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = RCloneVerbosity.Quiet;
                i2++;
            }
            else if (tok.StartsWith("--", StringComparison.Ordinal))
            {
                // Check for --flag=value syntax
                var eqIdx = tok.IndexOf('=');
                string flagName;
                string? flagValue = null;

                if (eqIdx > 0)
                {
                    flagName = tok[..eqIdx];
                    flagValue = tok[(eqIdx + 1)..].Trim('"');
                    i2++;
                }
                else if (FlagsWithValue.Contains(tok) && i2 + 1 < tokens.Count)
                {
                    flagName = tok;
                    flagValue = tokens[i2 + 1];
                    i2 += 2;
                }
                else
                {
                    // Unknown flag without value — treat as extra flag
                    flagName = tok;
                    extraFlags.Add(tok);
                    i2++;
                    continue;
                }

                switch (flagName.ToLower())
                {
                    case "--transfers":
                        if (int.TryParse(flagValue, out var t)) transfers = t;
                        break;
                    case "--log-file":
                        logFile = flagValue;
                        break;
                    case "--min-age":
                        minAge = flagValue;
                        break;
                    case "--include":
                        if (flagValue != null) includePatterns.Add(flagValue);
                        break;
                    case "--exclude":
                        if (flagValue != null) excludePatterns.Add(flagValue);
                        break;
                    case "--config":
                        // Skip — EZRClone manages its own config
                        break;
                    default:
                        if (flagValue != null)
                            extraFlags.Add($"{flagName}={flagValue}");
                        else
                            extraFlags.Add(flagName);
                        break;
                }
            }
            else
            {
                positionalArgs.Add(tok);
                i2++;
            }
        }

        var job = new RCloneJob
        {
            Operation = operation switch
            {
                "copy" => RCloneOperation.Copy,
                "sync" => RCloneOperation.Sync,
                "move" => RCloneOperation.Move,
                "delete" => RCloneOperation.Delete,
                _ => RCloneOperation.Copy
            },
            Transfers = transfers,
            Verbosity = verbosity,
            CreateLogFile = logFile != null,
            LogFilePath = logFile,
            MinAge = minAge,
            ExtraFlags = extraFlags,
            IncludePatterns = includePatterns,
            ExcludePatterns = excludePatterns
        };

        if (job.Operation == RCloneOperation.Delete)
        {
            // Delete has a single target path
            if (positionalArgs.Count >= 1)
                ApplyPath(positionalArgs[0], p => job.SourcePath = p,
                    r => { job.SourceIsRemote = true; job.SourceRemoteName = r; });
        }
        else
        {
            // Copy/Sync/Move have source + destination
            if (positionalArgs.Count >= 1)
                ApplyPath(positionalArgs[0], p => job.SourcePath = p,
                    r => { job.SourceIsRemote = true; job.SourceRemoteName = r; });
            if (positionalArgs.Count >= 2)
                ApplyPath(positionalArgs[1], p => job.DestinationPath = p,
                    r => { job.DestinationIsRemote = true; job.DestinationRemoteName = r; });
        }

        return job;
    }

    private static void ApplyPath(string token, Action<string> setPath, Action<string> setRemote)
    {
        if (IsRemotePath(token))
        {
            var colonIdx = token.IndexOf(':');
            setRemote(token[..colonIdx]);
            setPath(token[(colonIdx + 1)..]);
        }
        else
        {
            setPath(token);
        }
    }

    private static bool IsRemotePath(string token)
    {
        // Remote paths look like "REMOTE:path" where REMOTE is letters/digits/hyphens/underscores
        // Drive letters like "C:" or "E:" have a single letter before the colon
        var colonIdx = token.IndexOf(':');
        if (colonIdx <= 0) return false;

        // Single letter before colon = Windows drive letter, not a remote
        if (colonIdx == 1 && char.IsLetter(token[0])) return false;

        // Check that everything before the colon is a valid remote name
        var prefix = token[..colonIdx];
        return RemoteNamePattern().IsMatch(prefix);
    }

    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var i = 0;

        while (i < line.Length)
        {
            // Skip whitespace
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            if (i >= line.Length) break;

            if (line[i] == '"')
            {
                // Quoted string
                i++; // skip opening quote
                var start = i;
                while (i < line.Length && line[i] != '"') i++;
                tokens.Add(line[start..i]);
                if (i < line.Length) i++; // skip closing quote
            }
            else
            {
                // Unquoted token — but handle --flag="value with spaces"
                var start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]))
                {
                    if (line[i] == '"')
                    {
                        // Embedded quote in token like --config="path"
                        i++;
                        while (i < line.Length && line[i] != '"') i++;
                        if (i < line.Length) i++; // skip closing quote
                    }
                    else
                    {
                        i++;
                    }
                }
                var token = line[start..i];
                // Strip surrounding quotes from embedded quoted values
                tokens.Add(token);
            }
        }

        return tokens;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_][\w\-]*$")]
    private static partial Regex RemoteNamePattern();
}
