namespace Toolbox.Data.Models;

public class SiteSetting
{
    public string Name { get; set; } = string.Empty;
    public bool IsShop { get; set; }
    public string? ShopYamlPath { get; set; }
    public string? ShopThemesPath { get; set; }
}

