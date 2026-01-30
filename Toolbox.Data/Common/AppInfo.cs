namespace Toolbox.Data.Common;

public class AppInfo
{
    public const int SingletonId = 1;
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime IisRestartTime { get; set; }
    public string? CurrentVersion { get; set; }
    public string? LastInstalledVersion { get; set; }
    public string? LastShownChangelogForVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AppInfo()
    {
        Id = SingletonId;
        StartTime = new DateTime(2020, 1, 1);
        CreatedAt = new DateTime(2020, 1, 1);
        UpdatedAt = new DateTime(2020, 1, 1);
    }
}
