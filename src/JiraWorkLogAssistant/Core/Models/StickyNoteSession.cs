namespace JiraWorkLogAssistant.Core.Models;

public enum SessionType { TaskSpecific, SmartDistribution }

public class StickyNoteSession
{
    public int Id { get; set; }
    public int? JiraIssueId { get; set; }
    public SessionType SessionType { get; set; }
    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
    public double WindowWidth { get; set; } = 260;
    public double WindowHeight { get; set; } = 300;
    public bool IsOpen { get; set; } = true;
    public bool IsAlwaysOnTop { get; set; }
    public int? OverallDurationMinutes { get; set; }

    /// <summary>When true, this note's work log is generated and submitted automatically at AutoLogTime.</summary>
    public bool AutoLog { get; set; }
    /// <summary>Time of day (local) the auto-log fires. Defaults to 18:00 when AutoLog is enabled.</summary>
    public TimeSpan AutoLogTime { get; set; } = new TimeSpan(18, 0, 0);

    /// <summary>RTF content of the writing panel for this session (preserves formatting across restarts).</summary>
    public string? DraftRtf { get; set; }
    /// <summary>Plain-text mirror of the draft, used by the background auto-logger (no UI thread).</summary>
    public string? DraftText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public JiraIssue? JiraIssue { get; set; }
}
