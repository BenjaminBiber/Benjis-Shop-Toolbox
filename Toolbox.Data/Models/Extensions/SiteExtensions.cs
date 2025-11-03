using Microsoft.Web.Administration;
namespace Toolbox.Data.Models.Extensions;

public static class SiteExtensions
{
    public static string GetSitePath(this Site site)
    {
        if (site == null)
        {
            return null;
        }
            
        var rootApp = site.Applications["/"];
        var vdir = rootApp.VirtualDirectories["/"];
        return vdir.PhysicalPath;
    }

    public static void DeleteAssetFolder(this Site site)
    {
        var shopPath = site.GetSitePath();
        var basePath = shopPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Directory.Exists(Path.Combine(basePath, "wwwroot", "assets")))
        {
            Directory.Delete(Path.Combine(basePath, "wwwroot", "assets"), true);
        }
    }

    public static bool IsShop(this Site site)
    {
        if (site == null)
        {
            return false;
        }
        var path =  GetSitePath(site);
        if (String.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.EndsWith(@"\Shop") && (File.Exists(path + @"\shop.yaml") && Directory.Exists(path + @"\Themes"));
    }
}