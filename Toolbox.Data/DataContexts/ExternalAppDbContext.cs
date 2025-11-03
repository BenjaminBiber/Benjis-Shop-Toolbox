using Microsoft.EntityFrameworkCore;
using Toolbox.Data.Common;
using Toolbox.Data.Models;
using Toolbox.Data.Models.ShopYaml;
using Toolbox.Data.ShopsystemModels;

namespace Toolbox.Data.DataContexts;

public class ExternalAppDbContext : DbContext
{
    public ExternalAppDbContext(DbContextOptions<ExternalAppDbContext> options) : base(options) { }

    public DbSet<Widget> Widgets => Set<Widget>();
    public DbSet<WidgetsDescription> WidgetsDescriptions => Set<WidgetsDescription>();
    public DbSet<CustomWidgetLocation> CustomWidgetLocations => Set<CustomWidgetLocation>();
    public DbSet<ObjectExtension> ObjectExtensions => Set<Toolbox.Data.ShopsystemModels.ObjectExtension>();
    public DbSet<WidgetType> WidgetTypes => Set<WidgetType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(b =>
        {
            b.HasOne(w => w.WidgetType)
             .WithMany()
             .HasForeignKey(w => new { w.WidgetTypeId, w.ShopId })
             .OnDelete(DeleteBehavior.NoAction);

            b.Navigation(w => w.WidgetType).AutoInclude();
        });

        base.OnModelCreating(modelBuilder);
    }
}
