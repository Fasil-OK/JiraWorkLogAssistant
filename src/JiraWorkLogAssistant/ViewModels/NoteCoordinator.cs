namespace JiraWorkLogAssistant.ViewModels;

/// <summary>
/// Shared, app-wide coordinator for open note windows. Hands each window a distinct
/// sticky-note color and tracks which Jira tasks are currently claimed so a task can't be
/// opened in two windows at once.
/// </summary>
public class NoteCoordinator
{
    // Classic sticky-note palette (tuned for the dark theme borders).
    private static readonly string[] Palette =
    {
        "#FBBF24", // amber
        "#34D399", // green
        "#60A5FA", // blue
        "#F472B6", // pink
        "#A78BFA", // violet
        "#FB923C", // orange
        "#2DD4BF", // teal
        "#F87171", // red
    };

    private readonly List<string> _usedColors = new();
    private readonly HashSet<int> _tasksInUse = new();
    private readonly object _lock = new();

    /// <summary>Raised whenever the set of in-use tasks changes, so windows can refresh dropdowns.</summary>
    public event Action? TasksInUseChanged;

    /// <summary>Current global auto-logging state, shared across windows (avoids stale per-scope DB reads).</summary>
    public bool AutoLogEnabled { get; set; }

    /// <summary>Raised when global settings (e.g. auto-logging) change, so open windows can refresh.</summary>
    public event Action? SettingsChanged;

    public void NotifySettingsChanged() => SettingsChanged?.Invoke();

    public string AcquireColor()
    {
        lock (_lock)
        {
            foreach (var c in Palette)
            {
                if (!_usedColors.Contains(c)) { _usedColors.Add(c); return c; }
            }
            // More windows than palette entries: cycle.
            var color = Palette[_usedColors.Count % Palette.Length];
            _usedColors.Add(color);
            return color;
        }
    }

    public void ReleaseColor(string? color)
    {
        if (string.IsNullOrEmpty(color)) return;
        lock (_lock) { _usedColors.Remove(color); }
    }

    /// <summary>Claim a task for a window. Returns false if another window already owns it.</summary>
    public bool TryClaimTask(int issueId)
    {
        lock (_lock)
        {
            if (_tasksInUse.Contains(issueId)) return false;
            _tasksInUse.Add(issueId);
        }
        TasksInUseChanged?.Invoke();
        return true;
    }

    public void ReleaseTask(int issueId)
    {
        bool removed;
        lock (_lock) { removed = _tasksInUse.Remove(issueId); }
        if (removed) TasksInUseChanged?.Invoke();
    }

    public bool IsTaskInUse(int issueId)
    {
        lock (_lock) { return _tasksInUse.Contains(issueId); }
    }
}
