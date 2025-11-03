using System.ComponentModel;
using System.Reflection;

namespace Toolbox.Data.Common;

public class ReloadTime
{
    public static readonly Dictionary<string, TimeSpan> ReloadTimes = new()
    {
        { "Nicht Neuladen", TimeSpan.Zero },
        { "3 sek", TimeSpan.FromSeconds(3) },
        { "5 sek", TimeSpan.FromSeconds(5) },
        { "10 sek", TimeSpan.FromSeconds(10) },
        { "15 sek", TimeSpan.FromSeconds(15) },
        { "30 sek", TimeSpan.FromSeconds(30) },
        { "1 min", TimeSpan.FromMinutes(1) },
        { "2 min", TimeSpan.FromMinutes(2) },
        { "5 min", TimeSpan.FromMinutes(5) },
        { "10 min", TimeSpan.FromMinutes(10) },
        { "15 min", TimeSpan.FromMinutes(15) },
        { "30 min", TimeSpan.FromMinutes(30) }
    };
}

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
}

public enum ReloadOption
{
    [Description("Alle Logs")]
    AlleLogs,

    [Description("Seit Start der Anwendung")]
    SeitStartDerAnwendung,

    [Description("Seit letztem Neuladen")]
    SeitLetztemNeuladen
}

