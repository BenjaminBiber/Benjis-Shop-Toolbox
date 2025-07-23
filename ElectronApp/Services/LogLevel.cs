namespace Benjis_Shop_Toolbox.Services
{
    using System.ComponentModel;

    public enum LogLevel
    {
        [Description("Alle")]
        All,

        [Description("Information")]
        Information,

        [Description("Warnung")]
        Warning,

        [Description("Fehler")]
        Error,

        [Description("Kritisch")]
        Critical
    }

}
