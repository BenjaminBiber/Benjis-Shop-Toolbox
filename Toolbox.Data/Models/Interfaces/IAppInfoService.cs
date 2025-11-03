using Toolbox.Data.Common;

namespace Toolbox.Data.Models.Interfaces;

public interface IAppInfoService
{
    Task<AppInfo> GetAsync(CancellationToken cancellationToken = default);
    Task SetStartTimeAsync(DateTime startTime, CancellationToken cancellationToken = default);
    Task SetLastRestartTimeAsync(DateTime restartTime, CancellationToken cancellationToken = default);
}

