namespace Toolbox.Data.Common;

public class AppInfo
{
    public const int SingletonId = 1;
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime IisRestartTime { get; set; }

    public AppInfo()
    {
        Id = SingletonId;
        StartTime = new DateTime(2020, 1, 1);
    }
}
