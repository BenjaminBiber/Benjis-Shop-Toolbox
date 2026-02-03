using System;
using System.Collections.Generic;
namespace Toolbox.Services;

public enum TutorialTaskType
{
    None,
    ClickTarget
}

public sealed record TutorialTask(TutorialTaskType Type, string? Instruction);

public sealed record TutorialStep(
    string Id,
    string Title,
    string Description,
    string? TargetSelector,
    TutorialTask Task,
    IReadOnlyList<string>? Highlights = null);

public sealed class TutorialService
{
    private readonly object _sync = new();
    private readonly HashSet<string> _completedTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TutorialStep> _steps;

    public event Action? StateChanged;
    public event Action<bool>? Completed;

    public TutorialService()
    {
        _steps = new List<TutorialStep>
        {
            new(
                "welcome",
                "Willkommen",
                "In wenigen Schritten lernst du die wichtigsten Funktionen der Toolbox.",
                null,
                new TutorialTask(TutorialTaskType.None, null),
                new[]
                {
                    "IIS Apps starten, stoppen, neustarten",
                    "Logs finden, filtern und verstehen",
                    "Wichtige Bereiche schnell erreichen"
                }),
            new(
                "iis-start",
                "IIS Aktionen",
                "Hier steuerst du deine IIS App direkt aus der Toolbox.",
                "[data-tutorial=\"iis-start\"]",
                new TutorialTask(TutorialTaskType.ClickTarget, "Klicke auf Start."),
                new[]
                {
                    "Status im linken Panel",
                    "Start, Neustart und Stop"
                }),
            new(
                "log-filter",
                "Logs filtern",
                "Filter helfen dir, schneller die richtige Meldung zu finden.",
                "[data-tutorial=\"log-filter\"]",
                new TutorialTask(TutorialTaskType.ClickTarget, "Öffne den Filter."),
                new[]
                {
                    "Level, Logname, Zeitraum",
                    "Filter lassen sich jederzeit zurücksetzen"
                }),
            new(
                "log-search",
                "Logs suchen",
                "Mit der Suche findest du Meldungen sofort.",
                ".tutorial-log-search",
                new TutorialTask(TutorialTaskType.ClickTarget, "Klicke in das Suchfeld."),
                new[]
                {
                    "Suchbegriffe werden hervorgehoben",
                    "Perfekt für schnelles Debugging"
                }),
            new(
                "navigation",
                "Navigation",
                "Über die Seitenleiste erreichst du alle Bereiche der Toolbox.",
                ".tutorial-nav",
                new TutorialTask(TutorialTaskType.None, null),
                new[]
                {
                    "Themes, Extensions, Shop, Einstellungen",
                    "Hilfe und Tutorial findest du unten links"
                })
        };
    }

    public bool IsActive { get; private set; }
    public int StepIndex { get; private set; }
    public TutorialStep CurrentStep => _steps[Math.Clamp(StepIndex, 0, _steps.Count - 1)];
    public IReadOnlyList<TutorialStep> Steps => _steps;

    public bool IsFirst => StepIndex <= 0;
    public bool IsLast => StepIndex >= _steps.Count - 1;

    public bool IsCurrentTaskCompleted => CurrentStep.Task.Type == TutorialTaskType.None
                                          || _completedTasks.Contains(CurrentStep.Id);

    public double Progress => _steps.Count == 0 ? 0 : (StepIndex + 1) / (double)_steps.Count * 100.0;

    public void Start(bool force = false)
    {
        lock (_sync)
        {
            if (IsActive && !force)
            {
                return;
            }

            IsActive = true;
            StepIndex = 0;
            _completedTasks.Clear();
        }

        StateChanged?.Invoke();
    }

    public void Skip()
    {
        Finish(true);
    }

    public void Finish(bool skipped)
    {
        lock (_sync)
        {
            IsActive = false;
        }

        StateChanged?.Invoke();
        Completed?.Invoke(skipped);
    }

    public void Next()
    {
        if (!IsCurrentTaskCompleted)
        {
            return;
        }

        if (IsLast)
        {
            Finish(false);
            return;
        }

        StepIndex++;
        StateChanged?.Invoke();
    }

    public void Back()
    {
        if (IsFirst)
        {
            return;
        }

        StepIndex--;
        StateChanged?.Invoke();
    }

    public void MarkTaskCompleted(string stepId)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            return;
        }

        if (_completedTasks.Add(stepId))
        {
            StateChanged?.Invoke();
        }
    }

    public void ResetTaskCompletion(string stepId)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            return;
        }

        if (_completedTasks.Remove(stepId))
        {
            StateChanged?.Invoke();
        }
    }
}
