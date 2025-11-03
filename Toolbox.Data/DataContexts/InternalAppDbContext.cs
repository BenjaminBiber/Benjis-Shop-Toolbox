using Microsoft.EntityFrameworkCore;
using Toolbox.Data.Common;
using Toolbox.Data.Models;
using Toolbox.Data.Models.ShopYaml;

namespace Toolbox.DataContexts;

public class InternalAppDbContext : DbContext
{
    public InternalAppDbContext(DbContextOptions<InternalAppDbContext> options) : base(options) { }

    public DbSet<ToolboxSettings> Settings => Set<ToolboxSettings>();
    public DbSet<AppInfo> AppInfos => Set<AppInfo>();
    public DbSet<ShopSetting> ShopSettings => Set<ShopSetting>();
    public DbSet<DatabaseConnection> ShopDatabaseConnections => Set<DatabaseConnection>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<ToolboxSettings>();
        
        e.ToTable("Settings");
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasCheckConstraint("CK_AppSettings_Singleton", "Id = 1");
        e.HasMany(x => x.ShopSettingsList).WithOne(x => x.ToolboxSettings);
        e.HasData(new ToolboxSettings());

        var info =  modelBuilder.Entity<AppInfo>();
        
        info.ToTable("AppInfo");  
        info.Property(x => x.Id).ValueGeneratedNever();
        info.HasCheckConstraint("CK_AppInfo_Singleton", "Id = 1");
        info.HasData(new AppInfo());
        
        var shopSettings =  modelBuilder.Entity<ShopSetting>();
        shopSettings.HasKey(x => x.Id);
        shopSettings.HasOne(x => x.ToolboxSettings).WithMany(x => x.ShopSettingsList);
        
        var databaseConnection = modelBuilder.Entity<DatabaseConnection>();
        databaseConnection.HasKey(x => x.Id);
    }
}

