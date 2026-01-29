using System.Diagnostics;

namespace Toolbox.Services;

public class EventLogService
{
    public IReadOnlyList<string> GetLogNames()
    {
        try
        {
            var logs = EventLog.GetEventLogs();
            var names = new List<string>(logs.Length);
            foreach (var log in logs)
            {
                try
                {
                    var name = log.Log;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name.Trim());
                    }
                }
                catch
                {
                    // ignore invalid log entries
                }
                finally
                {
                    log?.Dispose();
                }
            }

            return names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
