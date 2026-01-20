using System;

namespace Toolbox.Services;

public class UiThemeState
{
    private readonly object _sync = new();

    public event Action? StateChanged;

    public bool IsWin95Mode { get; private set; }

    public void SetWin95Mode(bool enabled)
    {
        var changed = false;
        lock (_sync)
        {
            if (IsWin95Mode != enabled)
            {
                IsWin95Mode = enabled;
                changed = true;
            }
        }

        if (changed)
        {
            StateChanged?.Invoke();
        }
    }
}
