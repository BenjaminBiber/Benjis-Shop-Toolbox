using Microsoft.Extensions.DependencyInjection;
using Toolbox.Data.Services;

namespace Toolbox.Services;

/// <summary>
/// Runs the staging-system sync automatically:
///   1. Once in the background shortly after app startup.
///   2. Every time a tracked pipeline completes successfully.
/// </summary>
public sealed class StagingAutoSyncService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public StagingAutoSyncService(IServiceScopeFactory scopeFactory, PipelineTrackingService tracker)
    {
        _scopeFactory = scopeFactory;
        tracker.PipelineCompleted += OnPipelineCompleted;

        // Run initial sync in background after a short delay so the app finishes starting first
        _ = RunInitialSyncAsync();
    }

    private async Task RunInitialSyncAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        await SyncAsync();
    }

    private void OnPipelineCompleted(TrackedPipeline pipeline)
    {
        if (pipeline.Status?.Succeeded == true)
            _ = SyncAsync();
    }

    private async Task SyncAsync()
    {
        if (!await _lock.WaitAsync(0))
            return; // already running, skip

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<StagingSystemSyncService>();
            await syncService.SyncAsync();
        }
        catch
        {
            // fire-and-forget: silently ignore errors
        }
        finally
        {
            _lock.Release();
        }
    }
}
