namespace Toolbox.Data.Models;

public class ExtensionInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool HasSolution { get; set; }
    public bool HasProjects { get; set; }
}

