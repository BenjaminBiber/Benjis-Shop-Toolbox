using MudBlazor;

namespace ElectronApp.Models;

public class ChatMessage
{
    public string Text { get; }
    public ChatBubblePosition Position { get; }
    public string Initials { get; }
    public string ImageSrc { get; }

    public ChatMessage(string text, ChatBubblePosition position, string initials = "", string imageSrc = "")
    {
        Text = text;
        Position = position;
        Initials = initials;
        ImageSrc = imageSrc;
    }
}