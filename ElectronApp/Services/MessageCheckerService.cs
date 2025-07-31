using System.Timers;
using ElectronApp.Models;
using Newtonsoft.Json;

namespace Benjis_Shop_Toolbox.Services;

public class MessageCheckerService : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly HttpClient _client = new();
    private System.Timers.Timer? _timer;

    private List<DataItem> _messages = new();
    public IReadOnlyList<DataItem> Messages => _messages;

    public bool HasNewMessages { get; private set; }

    public event Action? OnChange;

    private const string WorkflowUrl = "https://automation.benjaminbiber.de/webhook/c8500a7e-2e2d-4fdd-9d52-271eb2f15038";

    public MessageCheckerService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        StartTimer();
    }

    public async Task CheckNowAsync()
    {
        await FetchMessagesAsync();
    }

    private void StartTimer()
    {
        _timer?.Dispose();
        var seconds = _settingsService.Settings.MessageCheckSeconds;
        if (seconds > 0)
        {
            _timer = new System.Timers.Timer(seconds * 1000);
            _timer.Elapsed += async (_, _) => await FetchMessagesAsync();
            _timer.AutoReset = true;
            _timer.Start();
        }
    }

    private async Task FetchMessagesAsync()
    {
        try
        {
            var response = await _client.GetAsync(WorkflowUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var items = JsonConvert.DeserializeObject<List<DataItem>>(json);
            if (items != null)
            {
                bool changed = _messages.Count != items.Count || !_messages.SelectMany(m => m.content).SequenceEqual(items.SelectMany(m => m.content));
                _messages = items;
                if (changed)
                {
                    HasNewMessages = true;
                    NotifyStateChanged();
                }
            }
        }
        catch
        {
            // ignore errors
        }
    }

    public void MarkAsRead()
    {
        HasNewMessages = false;
        NotifyStateChanged();
    }

    public void UpdateInterval() => StartTimer();

    private void NotifyStateChanged() => OnChange?.Invoke();

    public ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
