using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;
using Toolbox.Data.Models.Logs;

namespace Toolbox.Data.Services;

public sealed class LogBatch
{
    public static readonly LogBatch Empty = new(Array.Empty<LogEntry>(), new Dictionary<string, long>());

    public LogBatch(IReadOnlyList<LogEntry> entries, IReadOnlyDictionary<string, long> lastRecordIds)
    {
        Entries = entries;
        LastRecordIds = lastRecordIds;
    }

    public IReadOnlyList<LogEntry> Entries { get; }
    public IReadOnlyDictionary<string, long> LastRecordIds { get; }
}

public class LogService
{
    private readonly List<string> _logNames;
    private static readonly HashSet<MessageType> MessageOnlyTypes = new() { MessageType.Message };

    public LogService(string logName)
    {
        _logNames = new List<string> { logName };
    }

    public LogService(IEnumerable<string> logNames)
    {
        _logNames = logNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        return GetLogsBatch(since, level).Entries;
    }

    public LogBatch GetLogsBatch(DateTime since, LogLevel level = LogLevel.All, IReadOnlyDictionary<string, long>? sinceRecordIds = null)
    {
        if (_logNames.Count == 0)
        {
            return LogBatch.Empty;
        }

        var entries = new List<LogEntry>();
        var lastRecordIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var logName in _logNames)
        {
            if (string.IsNullOrWhiteSpace(logName))
                continue;

            var sinceRecordId = TryGetRecordId(sinceRecordIds, logName);
            if (TryReadWithEventLogReader(logName, since, level, sinceRecordId, entries, out var maxRecordId))
            {
                if (maxRecordId.HasValue)
                {
                    lastRecordIds[logName] = maxRecordId.Value;
                }
                continue;
            }

            if (TryReadWithEventLog(logName, since, level, sinceRecordId, entries, out maxRecordId))
            {
                if (maxRecordId.HasValue)
                {
                    lastRecordIds[logName] = maxRecordId.Value;
                }
            }
        }

        var ordered = entries
            .OrderByDescending(e => e.Time)
            .ToList();

        return new LogBatch(ordered, lastRecordIds);
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

    private static LogLevel MapLevel(byte? level) => level switch
    {
        1 => LogLevel.Critical,
        2 => LogLevel.Error,
        3 => LogLevel.Warning,
        4 => LogLevel.Information,
        _ => LogLevel.Information
    };

    private static long? TryGetRecordId(IReadOnlyDictionary<string, long>? map, string logName)
    {
        if (map == null)
        {
            return null;
        }

        return map.TryGetValue(logName, out var id) ? id : null;
    }

    private static bool TryReadWithEventLogReader(
        string logName,
        DateTime since,
        LogLevel level,
        long? sinceRecordId,
        List<LogEntry> target,
        out long? maxRecordId)
    {
        maxRecordId = sinceRecordId;

        try
        {
            var query = BuildQuery(since, level, sinceRecordId);
            var logQuery = new EventLogQuery(logName, PathType.LogName, query)
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(logQuery);
            for (EventRecord? record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
            {
                using (record)
                {
                    var recordId = record.RecordId ?? 0;
                    if (recordId > (maxRecordId ?? 0))
                    {
                        maxRecordId = recordId;
                    }

                    var message = record.FormatDescription() ?? string.Empty;
                    var parsed = ParseLog(message);
                    var time = ExtractTimestamp(parsed.Metadata) ?? record.TimeCreated ?? DateTime.MinValue;

                    target.Add(CreateEntry(MapLevel(record.Level), message, parsed, time));
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildQuery(DateTime since, LogLevel level, long? sinceRecordId)
    {
        var conditions = new List<string>();

        if (sinceRecordId.HasValue && sinceRecordId.Value > 0)
        {
            conditions.Add($"EventRecordID > {sinceRecordId.Value}");
        }
        else if (since > DateTime.MinValue)
        {
            var utc = since.ToUniversalTime().ToString("o");
            conditions.Add($"TimeCreated[@SystemTime>='{utc}']");
        }

        if (level != LogLevel.All)
        {
            conditions.Add($"Level = {MapLevelValue(level)}");
        }

        if (conditions.Count == 0)
        {
            return "*";
        }

        return $"*[System[{string.Join(" and ", conditions)}]]";
    }

    private static int MapLevelValue(LogLevel level) => level switch
    {
        LogLevel.Critical => 1,
        LogLevel.Error => 2,
        LogLevel.Warning => 3,
        LogLevel.Information => 4,
        _ => 0
    };

    private bool TryReadWithEventLog(
        string logName,
        DateTime since,
        LogLevel level,
        long? sinceRecordId,
        List<LogEntry> target,
        out long? maxRecordId)
    {
        maxRecordId = sinceRecordId;

        try
        {
            using var log = new EventLog(logName);
            if (log.Entries.Count == 0)
            {
                return true;
            }

            for (var i = log.Entries.Count - 1; i >= 0; i--)
            {
                var entry = log.Entries[i];

                if (sinceRecordId.HasValue && entry.Index <= sinceRecordId.Value)
                {
                    break;
                }

                if (sinceRecordId == null && entry.TimeGenerated < since)
                {
                    break;
                }

                var entryLevel = MapEntryType(entry.EntryType);
                if (level != LogLevel.All && entryLevel != level)
                {
                    continue;
                }

                if (entry.Index > (maxRecordId ?? 0))
                {
                    maxRecordId = entry.Index;
                }

                var parsed = ParseLog(entry.Message);
                target.Add(CreateEntry(entryLevel, entry.Message, parsed, ExtractTimestamp(parsed.Metadata) ?? entry.TimeGenerated));
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static LogMessage ParseLog(string logText)
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

    private static LogEntry CreateEntry(LogLevel level, string message, LogMessage parsed, DateTime time)
    {
        var fullMessage = parsed.GetFormattedMessage(MessageOnlyTypes);
        if (string.IsNullOrWhiteSpace(fullMessage))
        {
            fullMessage = message ?? string.Empty;
        }

        return new LogEntry
        {
            Level = level,
            ParsedMessage = parsed,
            Message = message ?? string.Empty,
            FullMessage = fullMessage,
            SearchText = fullMessage.ToLowerInvariant(),
            Time = time
        };
    }
}

