using ElectronApp.Models;
using ElectronNET.API;
using MudBlazor;

namespace Benjis_Shop_Toolbox.Services
{
    public static class AppInfo
    {
        public static DateTime StartTime { get; internal set; }
        public static BrowserWindow Window { get; set; }

        public static List<ChatMessage> Messages { get; set; } = new List<ChatMessage>()
        {
            new("Wie kann ich dir weiterhelfen?", ChatBubblePosition.Start, "AI")
        };
    }
}
