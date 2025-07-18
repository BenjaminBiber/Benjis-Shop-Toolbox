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
    }
}
