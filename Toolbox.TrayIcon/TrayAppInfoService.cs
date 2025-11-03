using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Toolbox.Data.Common;
using Toolbox.Data.Models.Interfaces;
using Toolbox.DataContexts;

sealed class TrayAppInfoService : IAppInfoService
{
    private readonly InternalAppDbContext _db;
    public TrayAppInfoService(InternalAppDbContext db) => _db = db;

    public async Task<AppInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.Database.EnsureCreatedAsync(cancellationToken);
            var info = await _db.Set<AppInfo>().AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            return info ?? new AppInfo { StartTime = DateTime.Now };
        }
        catch { return new AppInfo { StartTime = DateTime.Now }; }
    }

    public async Task SetStartTimeAsync(DateTime startTime, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.Database.EnsureCreatedAsync(cancellationToken);
            var set = _db.Set<AppInfo>();
            var info = await set.FirstOrDefaultAsync(cancellationToken);
            if (info == null) { info = new AppInfo { StartTime = startTime }; await set.AddAsync(info, cancellationToken); }
            else { info.StartTime = startTime; set.Update(info); }
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch { }
    }

    public async Task SetLastRestartTimeAsync(DateTime restartTime, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.Database.EnsureCreatedAsync(cancellationToken);
            var set = _db.Set<AppInfo>();
            var info = await set.FirstOrDefaultAsync(cancellationToken);
            if (info != null) { info.IisRestartTime = restartTime; set.Update(info); await _db.SaveChangesAsync(cancellationToken); }
        }
        catch { }
    }
}
