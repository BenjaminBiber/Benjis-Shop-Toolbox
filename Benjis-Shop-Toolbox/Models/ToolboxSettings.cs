namespace Benjis_Shop_Toolbox.Models
{
    public class ToolboxSettings
    {
        public string IisAppName { get; set; } = "Shop_TemplateV4";
        public string LogName { get; set; } = "4SELLERS";
        public int AutoRefreshSeconds { get; set; } = 30;
        public bool AutoRefreshEnabled { get; set; } = false;
        public bool LoadOnStartup { get; set; } = true;
        public bool OnlySinceRestart { get; set; } = false;
    }
}
