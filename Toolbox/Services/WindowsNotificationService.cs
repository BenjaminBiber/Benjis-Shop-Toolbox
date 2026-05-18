using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Toolbox.Data.Services;

namespace Toolbox.Services;

public sealed class WindowsNotificationService : IDisposable
{
    private const string Aumid = "TeamShop.ShopToolbox";

    private readonly PipelineTrackingService _tracker;

    public WindowsNotificationService(PipelineTrackingService tracker)
    {
        _tracker = tracker;
        RegisterAumid();
        _tracker.PipelineCompleted += OnPipelineCompleted;
    }

    private static void RegisterAumid()
    {
        try
        {
            // Minimal AUMID registration required for unpackaged Win32 apps
            Registry.SetValue(
                @"HKEY_CURRENT_USER\SOFTWARE\Classes\AppUserModelId\" + Aumid,
                "DisplayName", "Shop Toolbox");
        }
        catch { /* best-effort */ }
    }

    private static void OnPipelineCompleted(TrackedPipeline p)
    {
        try
        {
            var succeeded = p.Status?.Succeeded == true;
            var title     = succeeded ? "Pipeline erfolgreich" : "Pipeline fehlgeschlagen";
            var body      = $"{p.VmName}  ·  {p.ProjectName}";

            var xml = new XmlDocument();
            xml.LoadXml($"""
                <toast duration="long">
                  <visual>
                    <binding template="ToastGeneric">
                      <text>{System.Security.SecurityElement.Escape(title)}</text>
                      <text>{System.Security.SecurityElement.Escape(body)}</text>
                    </binding>
                  </visual>
                  <audio src="ms-winsoundevent:Notification.{(succeeded ? "Default" : "IM")}" />
                </toast>
                """);

            ToastNotificationManager.CreateToastNotifier(Aumid).Show(new ToastNotification(xml));
        }
        catch { /* notifications are best-effort */ }
    }

    public void Dispose()
    {
        _tracker.PipelineCompleted -= OnPipelineCompleted;
    }
}
