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
        public string RepoPath { get; set; } = "/Pfad/zum/repo-ordner";
        public string ShopThemesPath { get; set; } = "/Pfad/zum/shop-themes-ordner";
    }
}
