using System.Collections.Generic;

namespace Benjis_Shop_Toolbox.Models
{
    public class ToolboxSettings
    {
        public string? IisAppName { get; set; }
        public string? LogName { get; set; } = "4SELLERS";
        public int AutoRefreshSeconds { get; set; }
        public bool AutoRefreshEnabled { get; set; }
        public bool LoadOnStartup { get; set; }
        public bool OnlySinceRestart { get; set; }
        public string RepoPath { get; set; }
        public string ShopThemesPath { get; set; }

        public string? ShopYamlPath { get; set; }
        public bool RestartShopOnThemeChange { get; set; }

        /// <summary>
        /// Delay in seconds between stopping and starting a site when restarting.
        /// Must be at least one second.
        /// </summary>
        public int RestartDelaySeconds { get; set; } = 3;

        /// <summary>
        /// Gibt an, ob aufeinanderfolgende identische Logeinträge gebündelt angezeigt werden sollen.
        /// </summary>
        public bool BundleLogs { get; set; }

        /// <summary>
        /// Optional grouping for IIS sites displayed on the Sites page.
        /// </summary>
        public List<SiteGroup> SiteGroups { get; set; } = new();

        /// <summary>
        /// Additional configuration per IIS site.
        /// </summary>
        public List<SiteSetting> SiteSettings { get; set; } = new();
    }
}
