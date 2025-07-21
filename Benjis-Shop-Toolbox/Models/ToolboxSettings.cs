namespace Benjis_Shop_Toolbox.Models
{
    public class ToolboxSettings
    {
        public string? IisAppName { get; set; }
        public string? LogName { get; set; }
        public int AutoRefreshSeconds { get; set; }
        public bool AutoRefreshEnabled { get; set; }
        public bool LoadOnStartup { get; set; }
        public bool OnlySinceRestart { get; set; }
        public string RepoPath { get; set; }
        public string ShopThemesPath { get; set; } 
        
        public string? ShopYamlPath { get; set; }
        public bool RestartShopOnThemeChange { get; set; }
    }
}
