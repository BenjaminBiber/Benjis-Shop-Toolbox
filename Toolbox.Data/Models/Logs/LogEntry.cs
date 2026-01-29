namespace Toolbox.Data.Models.Logs;

public class LogEntry
{
    public DateTime Time { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public LogMessage ParsedMessage { get; set; } = new LogMessage();
    public string FullMessage { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public int Count { get; set; } = 1;
}

