namespace Toolbox.Data.Models;

public class RepoAction
{
    public string Label { get; set; } = string.Empty;
    public Func<string, Task<bool>>? ExecuteAsync { get; set; }
}

