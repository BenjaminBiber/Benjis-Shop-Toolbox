namespace Toolbox.Data.Models;

public class ThemeInfo
{
    public ThemeInfo(string name, string path, bool linkExists, string repo, string themeFolder, bool isThemeOverwrite = false)
    {
        Name = name;
        Path = path;
        LinkExists = linkExists;
        Repo = repo;
        ThemeFolder = themeFolder;
        IsThemeOverwrite = isThemeOverwrite;
    }

    public ThemeInfo() : this(string.Empty, string.Empty, false, string.Empty, string.Empty, false) {}

    public string Name { get; set; }
    public string Path { get; set; }
    public bool LinkExists { get; set; }
    public string Repo { get; set; }
    public string ThemeFolder { get; set; }
    public bool IsThemeOverwrite { get; set; }
}

