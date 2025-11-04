namespace Toolbox.Data.Models;

public class ActionOption
{
    public string Label { get; set; } = string.Empty;
    public bool Selected { get; set; }
    public Func<string, Task<bool>>? ExecuteAsync { get; set; }
}