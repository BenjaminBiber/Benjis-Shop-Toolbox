using Toolbox.Data.Models.ShopYaml;
using Toolbox.Data.Services;

namespace Toolbox.Data.Models;

public class ShopSetting
{
    public int Id { get; set; }
    public long SiteId { get; set; }
    public string ThemeFolderPath { get; set; } = string.Empty;
    public string ShopYamlPath { get; set; } = string.Empty;
    
    public ToolboxSettings ToolboxSettings { get; set; } = default!;

    public ShopsystemConfig GetShopYamlContent()
    {
        return ShopYamlService.LoadConfiguration(ShopYamlPath);
    }

    public void OpenInVsc()
    {
        ShopYamlService.OpenYamlInVSCode(ShopYamlPath);
    }

    public string? GetConnectionString()
    {
        var path = ShopYamlPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            var config =  ShopYamlService.LoadConfiguration(path);
            if (config == null || config.ZionConfiguration.DatabaseConnections == null ||
                config.ZionConfiguration.DatabaseConnections.Count == 0)
            {
                return string.Empty;
            }
            return config.ZionConfiguration.DatabaseConnections.FirstOrDefault().GetConnectionString();
        }
        return null;
    }
    
    public void OpenInExplorer()
    {
        var path = ShopYamlPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
        }
    }
    
    public DatabaseConnection GetConnection()
    {
        var path = ShopYamlPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            var config =  ShopYamlService.LoadConfiguration(path);
            if (config == null || config.ZionConfiguration.DatabaseConnections == null ||
                config.ZionConfiguration.DatabaseConnections.Count == 0)
            {
                return new DatabaseConnection();
            }
            return config.ZionConfiguration.DatabaseConnections.FirstOrDefault();
        }
        return null;
    }
}
