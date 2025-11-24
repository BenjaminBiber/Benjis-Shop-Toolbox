using System;

namespace Toolbox.Services;

public class EasterEggService
{
    private readonly object _sync = new();

    public event Action? StateChanged;

    public bool IsSecretUnlocked { get; private set; }
    public bool IsEasterEggCompleted { get; private set; }

    public bool ShouldShowHomeButton => IsSecretUnlocked && !IsEasterEggCompleted;

    public void UnlockSecret()
    {
        var changed = false;
        lock (_sync)
        {
            if (!IsSecretUnlocked)
            {
                IsSecretUnlocked = true;
                changed = true;
            }
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    public void MarkEasterEggCompleted()
    {
        var changed = false;
        lock (_sync)
        {
            if (!IsEasterEggCompleted)
            {
                IsEasterEggCompleted = true;
                changed = true;
            }
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    private void NotifyChanged() => StateChanged?.Invoke();
}
