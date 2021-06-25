using Fan.Medias;

namespace Fan.Settings
{
    public class AppSettings
    {
        public EPreferredDomain PreferredDomain { get; set; } = EPreferredDomain.Auto;

        public EMediaStorageType MediaStorageType { get; set; } = EMediaStorageType.FileSystem;

        public string MediaContainerName { get; set; } = "media";
    }
}