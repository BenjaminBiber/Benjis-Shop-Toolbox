using Microsoft.EntityFrameworkCore;
using Toolbox.Data.Common;
using Toolbox.Data.DataContexts;
using Toolbox.Data.Models.Interfaces;

namespace Toolbox.Services;

public class AppInfoService : IAppInfoService
{
    private readonly InternalAppDbContext? _db;

    public AppInfoService(InternalAppDbContext? db = null)
    {
        _db = db;
    }

    public async Task<AppInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_db == null)
        {
            return new AppInfo
            {
                StartTime = DateTime.Now,
            };
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        var info = await _db.Set<AppInfo>().AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return info ?? new AppInfo
        {
            StartTime = DateTime.Now,
        };
    }

    public async Task SetStartTimeAsync(DateTime startTime, CancellationToken cancellationToken = default)
    {
        if (_db == null)
        {
            return;
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        var set = _db.Set<AppInfo>();
        var info = await set.FirstOrDefaultAsync(cancellationToken);
        if (info == null)
        {
            info = new AppInfo { StartTime = startTime};
            await set.AddAsync(info, cancellationToken);
        }
        else
        {
            info.StartTime = startTime;
            set.Update(info);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
    
    public async Task SetLastRestartTimeAsync(DateTime restartTime, CancellationToken cancellationToken = default)
    {
        if (_db == null)
        {
            return;
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        var set = _db.Set<AppInfo>();
        var info = await set.FirstOrDefaultAsync(cancellationToken);
        if (info != null)
        {
            info.IisRestartTime = restartTime;
            set.Update(info);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}
