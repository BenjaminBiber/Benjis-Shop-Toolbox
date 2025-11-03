using System.ComponentModel;

namespace Toolbox.Data.Models.Logs;

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

