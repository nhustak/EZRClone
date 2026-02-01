using EZRClone.Models;

namespace EZRClone.Services;

public interface IAppSettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
