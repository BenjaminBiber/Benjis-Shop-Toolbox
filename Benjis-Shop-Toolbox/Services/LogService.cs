using System.Diagnostics;

namespace Benjis_Shop_Toolbox.Services
{
    public class LogEntry
    {
        public DateTime Time { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class LogService
    {
        private readonly string _logName;

        public LogService(string logName)
        {
            _logName = logName;
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
                    Time = e.TimeGenerated,
                    Level = MapEntryType(e.EntryType),
                    Message = e.Message
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
    }
}
