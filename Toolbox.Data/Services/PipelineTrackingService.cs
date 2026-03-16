using Microsoft.Extensions.DependencyInjection;

namespace Toolbox.Data.Services;

public class TrackedPipeline
{
    public int    BuildId     { get; init; }
    public string ProjectUrl  { get; init; } = "";
    public string ProjectName { get; init; } = "";
    public string VmName      { get; init; } = "";
    public DateTime StartedAt { get; init; } = DateTime.Now;
    public PipelineBuildStatus? Status { get; set; }
}

public class PipelineTrackingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly List<TrackedPipeline> _pipelines = new();
    private Task? _pollingTask;

    public PipelineTrackingService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyList<TrackedPipeline> Pipelines => _pipelines;

    public event Action? Changed;
    public event Action<TrackedPipeline>? PipelineCompleted;

    public void Add(TrackedPipeline pipeline)
    {
        _pipelines.Insert(0, pipeline);
        Changed?.Invoke();
        EnsurePolling();
    }

    public void UpdateStatus(int buildId, PipelineBuildStatus status)
    {
        var p = _pipelines.FirstOrDefault(x => x.BuildId == buildId);
        if (p == null) return;

        var wasRunning = p.Status == null || p.Status.IsRunning;
        p.Status = status;

        if (wasRunning && status.IsCompleted)
            PipelineCompleted?.Invoke(p);

        Changed?.Invoke();
    }

    public int RunningCount => _pipelines.Count(p =>
        p.Status == null || p.Status.IsRunning);

    // ── Background polling ────────────────────────────────────────────────────

    private void EnsurePolling()
    {
        if (_pollingTask == null || _pollingTask.IsCompleted)
            _pollingTask = PollLoopAsync();
    }

    private async Task PollLoopAsync()
    {
        while (true)
        {
            await Task.Delay(3000);

            var running = _pipelines
                .Where(p => p.Status == null || p.Status.IsRunning)
                .ToList();

            if (running.Count == 0) break;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var buildService = scope.ServiceProvider.GetRequiredService<TfsBuildService>();

                foreach (var p in running)
                {
                    try
                    {
                        var status = await buildService.GetBuildStatusAsync(p.ProjectUrl, p.BuildId);
                        UpdateStatus(p.BuildId, status);
                    }
                    catch { /* skip individual failures, keep polling */ }
                }
            }
            catch { /* scope creation failed, retry next interval */ }
        }
    }
}
