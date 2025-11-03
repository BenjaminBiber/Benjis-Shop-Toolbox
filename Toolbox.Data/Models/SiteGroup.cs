namespace Toolbox.Data.Models;

public class SiteGroup
{
    public string Name { get; set; } = string.Empty;
    public List<string> Sites { get; set; } = new();
}

