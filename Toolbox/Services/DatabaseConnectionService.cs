using System.Linq;
using System.Collections.Generic;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Toolbox.Data.DataContexts;
using Toolbox.Data.Models.ShopYaml;
using Toolbox.Data.Services;

namespace Toolbox.Services;

public class DatabaseConnectionService
{
    private readonly InternalAppDbContext _db;
    public List<DatabaseConnection> DatabaseConnections;
    private readonly SettingsService _settings;
    
    public DatabaseConnectionService(InternalAppDbContext db,  SettingsService settings)
    {
        _db = db;
        _settings = settings;
        GetConnections();
    }

    public void SaveChanges()
    {
        if (_db == null) return;

        var keepIds = DatabaseConnections
            .Where(x => x.Id != 0)
            .Select(x => x.Id)
            .ToHashSet();

        var deleteStubs = _db.ShopDatabaseConnections
            .Where(e => !keepIds.Contains(e.Id))
            .Select(e => new DatabaseConnection { Id = e.Id })
            .ToList();

        if (deleteStubs.Count > 0)
            _db.ShopDatabaseConnections.RemoveRange(deleteStubs);

        foreach (var item in DatabaseConnections)
        {
            if (item.Id == 0)
            {
                _db.ShopDatabaseConnections.Add(item);
            }
            else
            {
                _db.ShopDatabaseConnections.Attach(item);
            }
        }

        _db.SaveChanges();
    }

    public void FillDataBaseConnections()
    {
        var added = 0;
        var foundYaml = false;

        try
        {
            var root = _settings.Settings.GeneralFolderPath;
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                foreach (var yaml in EnumerateShopYamlFiles(root))
                {
                    foundYaml = true;
                    try
                    {
                        var config = ShopYamlService.LoadConfiguration(yaml);
                        var connection = config?.ZionConfiguration?.DatabaseConnections?.FirstOrDefault();
                        if (connection == null) continue;
                        if (!DatabaseConnections.Contains(connection))
                        {
                            DatabaseConnections.Add(connection);
                            added++;
                        }
                    }
                    catch
                    {
                        // ignore single yaml errors and continue
                    }
                }
            }
        }
        catch
        {
            // ignore scanning errors
        }

        if (!foundYaml)
        {
            var shops = _settings.Settings.ShopSettingsList;
            foreach (var shop in shops)
            {
                try
                {
                    var config = shop.GetShopYamlContent();
                    var connection = config?.ZionConfiguration?.DatabaseConnections?.FirstOrDefault();
                    if (connection == null) continue;
                    if (!DatabaseConnections.Contains(connection))
                    {
                        DatabaseConnections.Add(connection);
                        added++;
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        SaveChanges();
    }

    private static IEnumerable<string> EnumerateShopYamlFiles(string root)
    {
        var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".svn", ".hg", ".vs", ".idea", ".vscode",
            "bin", "obj", "packages", "node_modules"
        };

        var q = new Queue<string>();
        q.Enqueue(root);
        while (q.Count > 0)
        {
            var dir = q.Dequeue();
            string[] files;
            try { files = Directory.GetFiles(dir, "*.yaml", SearchOption.TopDirectoryOnly); }
            catch { files = Array.Empty<string>(); }

            foreach (var f in files)
            {
                if (string.Equals(Path.GetFileName(f), "shop.yaml", StringComparison.OrdinalIgnoreCase))
                    yield return f;
            }

            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly); }
            catch { subdirs = Array.Empty<string>(); }

            foreach (var sub in subdirs)
            {
                var name = Path.GetFileName(sub);
                if (ignore.Contains(name)) continue;
                q.Enqueue(sub);
            }
        }
    }
    
    private void GetConnections()
    {
        if (_db == null)
        {
            return;
        }
        _db.Database.EnsureCreated();
        DatabaseConnections = _db.ShopDatabaseConnections.AsNoTracking().ToList();
        return;
    }
}
