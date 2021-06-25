using System.Threading.Tasks;

namespace Fan.Settings
{
    public interface ISettingService
    {
        Task<T> GetSettingsAsync<T>() where T : class, ISettings, new();
        /// <summary>
        Task<T> UpsertSettingsAsync<T>(T settings) where T : class, ISettings, new();
    }
}
