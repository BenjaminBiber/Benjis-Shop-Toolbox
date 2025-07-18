using System.Diagnostics;

namespace Benjis_Shop_Toolbox.Services
{
    public class LogEntry
    {
        public DateTime Time { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class LogService
    {
        private readonly string _logName;

        public LogService(string logName)
        {
            _logName = logName;
        }

        public IEnumerable<LogEntry> GetLogs(DateTime since, string? level = null)
        {
            using var log = new EventLog(_logName);
            var entries = log.Entries.Cast<EventLogEntry>()
                .Where(e => e.TimeGenerated >= since);

            if (!string.IsNullOrEmpty(level) && level != "All")
            {
                entries = entries.Where(e => e.EntryType.ToString().Equals(level, StringComparison.OrdinalIgnoreCase));
            }

            return entries
                .OrderByDescending(e => e.TimeGenerated)
                .Select(e => new LogEntry
                {
                    Time = e.TimeGenerated,
                    Level = e.EntryType.ToString(),
                    Message = e.Message
                })
                .ToList();
        }
    }
}
