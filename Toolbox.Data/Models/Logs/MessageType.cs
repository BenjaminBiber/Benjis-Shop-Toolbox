using System.ComponentModel;

namespace Toolbox.Data.Models.Logs;

public enum MessageType
{
    [Description("Meta Daten")]
    Metadata,

    [Description("Meldung")]
    Message,

    [Description("Herkunft")]
    Origin
}

