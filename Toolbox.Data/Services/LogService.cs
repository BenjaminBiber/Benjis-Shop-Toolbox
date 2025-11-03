using System.Diagnostics;
using System.Text.RegularExpressions;
using Toolbox.Data.Models.Logs;

namespace Toolbox.Data.Services;

public class LogService
{
    private readonly string _logName;

    public LogService(string logName)
    {
        _logName = logName;
    }

    public static DateTime? ExtractTimestamp(string logText)
    {
        var regex = new Regex(@"Timestamp:\s*(\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2}\.\d{3})");
        var match = regex.Match(logText);
        if (match.Success)
        {
            string timestampStr = match.Groups[1].Value;
            if (DateTime.TryParseExact(timestampStr, "yyyy.MM.dd HH:mm:ss.fff", null, System.Globalization.DateTimeStyles.None, out var timestamp))
            {
                return timestamp;
            }
        }
        return null;
    }

    public IEnumerable<LogEntry> GetLogs(DateTime since, LogLevel level = LogLevel.All)
    {
        using var log = new EventLog(_logName);
        var entries = log.Entries.Cast<EventLogEntry>()
            .Where(e => e.TimeGenerated >= since);

        if (level != LogLevel.All)
        {
            entries = entries.Where(e => MapEntryType(e.EntryType) == level);
        }

        return entries
            .OrderByDescending(e => e.TimeGenerated)
            .Select(e => new LogEntry
            {
                Level = MapEntryType(e.EntryType),
                ParsedMessage = ParseLog(e.Message),
                Message = e.Message,
                Time = ExtractTimestamp(ParseLog(e.Message).Metadata) ?? e.TimeGenerated
            })
            .ToList();
    }

    private static LogLevel MapEntryType(EventLogEntryType type) => type switch
    {
        EventLogEntryType.Error => LogLevel.Error,
        EventLogEntryType.Warning => LogLevel.Warning,
        EventLogEntryType.Information => LogLevel.Information,
        EventLogEntryType.FailureAudit => LogLevel.Error,
        EventLogEntryType.SuccessAudit => LogLevel.Information,
        _ => LogLevel.Information
    };

    public LogMessage ParseLog(string logText)
    {
        var pattern = @"^(?:(?<Origin>(ServiceId|ShopId):\s+.+?)\r?\n)?\s*Metadata:\s*(?<Metadata>.+?)\r?\n\s*Message:\s*(?<Message>.+)$";
        var regex = new Regex(pattern, RegexOptions.Singleline);
        var match = regex.Match(logText);

        if (!match.Success)
        {
            return new LogMessage()
            {
                Message = logText,
            };
        }

        return new LogMessage()
        {
            Origin = match.Groups["Origin"].Success ? match.Groups["Origin"].Value : string.Empty,
            Metadata = match.Groups["Metadata"].Success ? match.Groups["Metadata"].Value.Replace("\t", "") : string.Empty,
            Message = match.Groups["Message"].Value
        };
    }
}

