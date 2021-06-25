using Fan.Navigation;

namespace Fan.Settings
{
    /// <summary>
    /// Core settings for the site.
    /// </summary>
    public class CoreSettings : ISettings
    {
        public string Title { get; set; } = "Blogs App";
        public string Tagline { get; set; } = "";
        public string Theme { get; set; } = "Clarity";

        public string TimeZoneId { get; set; } = "UTC";

        public string GoogleAnalyticsTrackingID { get; set; }

        public bool SetupDone { get; set; } = false;

        public Nav Home { get; set; } = new Nav { Id = App.BLOG_APP_ID, Type = ENavType.App };
    }
}
