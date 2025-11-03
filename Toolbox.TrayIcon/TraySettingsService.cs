using Microsoft.EntityFrameworkCore;
using Microsoft.Web.Administration;
using Toolbox.Data.DataContexts;
using Toolbox.Data.Models;
using Toolbox.Data.Models.Interfaces;

sealed class TraySettingsService : ISettingsService
{
    private readonly InternalAppDbContext _db;
    public ToolboxSettings Settings { get; private set; } = new();

    public TraySettingsService(InternalAppDbContext db)
    {
        _db = db;
        try
        {
            _db.Database.EnsureCreated();
            Settings = _db.Settings.Include(x => x.ShopSettingsList).FirstOrDefault() ?? new ToolboxSettings();

            if (string.IsNullOrWhiteSpace(Settings.IisAppName))
            {
                try
                {
                    using var manager = new ServerManager();
                    var started = manager.Sites.FirstOrDefault(x => x.State == ObjectState.Started)?.Name;
                    if (!string.IsNullOrWhiteSpace(started))
                    {
                        Settings.IisAppName = started;
                        try { _db.Update(Settings); _db.SaveChanges(); } catch { }
                    }
                }
                catch { }
            }
        }
        catch { Settings = new ToolboxSettings(); }
    }
}

