using System;

namespace Toolbox.Services;

public enum SlotOutcome
{
    None,
    Combo,
    Jackpot
}

public class EasterEggService
{
    private readonly object _sync = new();

    public event Action? StateChanged;

    public bool IsSecretUnlocked { get; private set; }
    public bool IsEasterEggCompleted { get; private set; }
    public int SlotPoints { get; private set; }
    public int SlotCombo { get; private set; }
    public int SlotBestCombo { get; private set; }
    public int SlotJackpots { get; private set; }
    public int SlotCoins { get; private set; } = 100;
    public int SlotFreeSpins { get; private set; }

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

    public bool TrySpendSlotCoins(int bet)
    {
        if (bet <= 0) return false;
        var changed = false;
        lock (_sync)
        {
            if (SlotCoins < bet) return false;
            SlotCoins -= bet;
            changed = true;
        }

        if (changed)
        {
            NotifyChanged();
        }

        return true;
    }

    public bool TryConsumeFreeSpin()
    {
        var changed = false;
        lock (_sync)
        {
            if (SlotFreeSpins <= 0) return false;
            SlotFreeSpins -= 1;
            changed = true;
        }

        if (changed)
        {
            NotifyChanged();
        }

        return true;
    }

    public void AwardFreeSpin(int count = 1)
    {
        if (count <= 0) return;
        var changed = false;
        lock (_sync)
        {
            SlotFreeSpins += count;
            changed = true;
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    public void RegisterSlotSpin(SlotOutcome outcome, int bet)
    {
        var changed = false;
        lock (_sync)
        {
            var payout = outcome switch
            {
                SlotOutcome.Jackpot => bet * 10,
                SlotOutcome.Combo => bet * 2,
                _ => 0
            };

            if (payout > 0)
            {
                SlotCoins += payout;
                changed = true;
            }

            switch (outcome)
            {
                case SlotOutcome.Jackpot:
                    SlotPoints += 10;
                    SlotCombo += 1;
                    SlotJackpots += 1;
                    changed = true;
                    break;
                case SlotOutcome.Combo:
                    SlotPoints += 4;
                    SlotCombo += 1;
                    changed = true;
                    break;
                case SlotOutcome.None:
                    if (SlotCombo != 0)
                    {
                        SlotCombo = 0;
                        changed = true;
                    }
                    break;
            }

            if (SlotCombo > SlotBestCombo)
            {
                SlotBestCombo = SlotCombo;
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
