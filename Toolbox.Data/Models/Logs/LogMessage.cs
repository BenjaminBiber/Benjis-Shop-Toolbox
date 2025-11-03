using Toolbox.Data.Models.Logs;

namespace Toolbox.Data.Models.Logs;

public class LogMessage
{
    public string Origin { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrEmpty(Message) && !string.IsNullOrEmpty(Metadata);

    public string GetFormattedMessage(HashSet<MessageType> messageTypes)
    {
        var formattedMessage = string.Empty;

        if (messageTypes.Contains(MessageType.Origin) && !string.IsNullOrEmpty(Origin))
        {
            formattedMessage += $"Origin: {Origin}\n";
        }

        if (messageTypes.Contains(MessageType.Metadata) && !string.IsNullOrEmpty(Metadata))
        {
            formattedMessage += $"Metadata: {Metadata}\n";
        }

        if (messageTypes.Contains(MessageType.Message) && !string.IsNullOrEmpty(Message))
        {
            formattedMessage += $"Message: {Message}";
        }

        return formattedMessage.Trim();
    }
}

