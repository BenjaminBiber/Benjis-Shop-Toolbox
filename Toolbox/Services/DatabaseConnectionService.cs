using System.Linq;
using Microsoft.EntityFrameworkCore;
using Toolbox.Data.Models.ShopYaml;
using Toolbox.Data.Services;
using Toolbox.DataContexts;

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
        var shops = _settings.Settings.ShopSettingsList;
        foreach (var shop in shops)
        {
            var config = shop.GetShopYamlContent();
            var connection = config.ZionConfiguration.DatabaseConnections.FirstOrDefault();
            if (connection == null)
            {
                return;
            }

            if (!DatabaseConnections.Contains(connection))
            {
                DatabaseConnections.Add(connection);
            }
        }
        SaveChanges();
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
