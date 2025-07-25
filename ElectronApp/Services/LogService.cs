using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Benjis_Shop_Toolbox.Services
{
    public class LogEntry
    {
        public DateTime Time { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public LogMessage ParsedMessage { get; set; } = new LogMessage();
        /// <summary>
        /// Anzahl gebündelter Logeinträge.
        /// </summary>
        public int Count { get; set; } = 1;
    }

    public class LogMessage
    {
        public string Origin { get; set; } = string.Empty;
        public string Metadata { get; set; } = string.Empty;
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
            ParseLog(entries.First().Message);
            return entries
                .OrderByDescending(e => e.TimeGenerated)
                .Select(e => new LogEntry
                {
                    Time = e.TimeGenerated,
                    Level = MapEntryType(e.EntryType),
                    ParsedMessage = ParseLog(e.Message),
                    Message = e.Message,
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
                throw new ArgumentException("Log format not recognized");

            return new LogMessage()
            {
                Origin = match.Groups["Origin"].Success ? match.Groups["Origin"].Value : string.Empty,
                Metadata = match.Groups["Metadata"].Success ? match.Groups["Metadata"].Value.Replace("\t", "") : string.Empty,
                Message = match.Groups["Message"].Value
            };
        }
    }
}
