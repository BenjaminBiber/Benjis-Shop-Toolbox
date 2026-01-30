using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Toolbox.Data.Common;
using Toolbox.Data.DataContexts;
using Toolbox.Data.Models.Interfaces;

sealed class TrayAppInfoService : IAppInfoService
{
    private readonly InternalAppDbContext _db;
    public TrayAppInfoService(InternalAppDbContext db) => _db = db;

    public async Task<AppInfo> GetAsync(CancellationToken cancellationToken = default)
        => await GetOrCreateAsync(cancellationToken);

    public async Task<AppInfo> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.Database.EnsureCreatedAsync(cancellationToken);
            var set = _db.Set<AppInfo>();
            var info = await set.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (info != null)
            {
                return info;
            }

            var now = DateTime.UtcNow;
            info = new AppInfo
            {
                StartTime = DateTime.Now,
                CreatedAt = now,
                UpdatedAt = now
            };
            await set.AddAsync(info, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return info;
        }
        catch { return new AppInfo { StartTime = DateTime.Now, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }; }
    }

    public async Task UpdateAsync(AppInfo appInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.Database.EnsureCreatedAsync(cancellationToken);
            var set = _db.Set<AppInfo>();
            var existing = await set.FirstOrDefaultAsync(cancellationToken);

            var now = DateTime.UtcNow;
            appInfo.Id = AppInfo.SingletonId;
            if (appInfo.CreatedAt <= new DateTime(2000, 1, 1) ||
                appInfo.CreatedAt == new DateTime(2020, 1, 1))
            {
                appInfo.CreatedAt = now;
            }
            appInfo.UpdatedAt = now;

            if (existing == null)
            {
                await set.AddAsync(appInfo, cancellationToken);
            }
            else
            {
                existing.StartTime = appInfo.StartTime;
                existing.IisRestartTime = appInfo.IisRestartTime;
                existing.CurrentVersion = appInfo.CurrentVersion;
                existing.LastInstalledVersion = appInfo.LastInstalledVersion;
                existing.LastShownChangelogForVersion = appInfo.LastShownChangelogForVersion;
                existing.CreatedAt = appInfo.CreatedAt;
                existing.UpdatedAt = appInfo.UpdatedAt;
                set.Update(existing);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch { }
    }

    public async Task SetStartTimeAsync(DateTime startTime, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.Database.EnsureCreatedAsync(cancellationToken);
            var set = _db.Set<AppInfo>();
            var info = await set.FirstOrDefaultAsync(cancellationToken);
            if (info == null)
            {
                var now = DateTime.UtcNow;
                info = new AppInfo
                {
                    StartTime = startTime,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await set.AddAsync(info, cancellationToken);
            }
            else
            {
                info.StartTime = startTime;
                info.UpdatedAt = DateTime.UtcNow;
                set.Update(info);
            }
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
            if (info != null)
            {
                info.IisRestartTime = restartTime;
                info.UpdatedAt = DateTime.UtcNow;
                set.Update(info);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        catch { }
    }
}
