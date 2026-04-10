using System.IO;
using System.Text.Json;
using EZRClone.Models;
using HotCoreUtility.RClone;

namespace EZRClone.Services;

public class AppSettingsService : IAppSettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EZRClone");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return Normalize(new AppSettings());

        var json = File.ReadAllText(SettingsPath);
        return Normalize(JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings());
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.OperationProfiles ??= new RCloneOperationProfileSet();

        if (settings.DefaultTransfers > 0)
            settings.OperationProfiles.Download.Transfers = settings.DefaultTransfers;

        if (settings.DefaultCheckers > 0)
            settings.OperationProfiles.Download.Checkers = settings.DefaultCheckers;

        if (!string.IsNullOrWhiteSpace(settings.DefaultDownloadExtraFlags))
            settings.OperationProfiles.Download.ExtraFlagsText = settings.DefaultDownloadExtraFlags;

        return settings;
    }
}
