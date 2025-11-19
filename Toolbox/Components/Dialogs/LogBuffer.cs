using System.Text;

namespace Toolbox.Components.Dialogs;

public class LogBuffer
{
    private readonly StringBuilder _builder = new();
    private readonly object _syncRoot = new();

    public event Action? Changed;

    public string Text
    {
        get
        {
            lock (_syncRoot)
            {
                return _builder.ToString();
            }
        }
    }

    public void Append(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        lock (_syncRoot)
        {
            _builder.Append(text);
        }

        Changed?.Invoke();
    }

    public void AppendLine(string? text)
    {
        Append(text + Environment.NewLine);
    }
}

