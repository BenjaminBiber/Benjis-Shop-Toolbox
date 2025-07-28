using System.ComponentModel;

namespace ElectronApp.Models;

public enum MessageType
{
    [Description("Meta Daten")]
    Metadata,

    [Description("Meldung")]
    Message,

    [Description("Herkunft")]
    Origin
    
}