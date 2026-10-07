namespace JiraWorkLogAssistant.Core.Models;

public enum NoteStatus { Active, Logged, Archived }

public class WorkNote
{
    public int Id { get; set; }
    public int? JiraIssueId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public NoteStatus Status { get; set; } = NoteStatus.Active;
    public bool IsSmartDistribution { get; set; }
    public int? MatchedIssueId { get; set; }
    public double? MatchConfidence { get; set; }
    public int? WorkLogId { get; set; }

    public JiraIssue? JiraIssue { get; set; }
    public WorkLog? WorkLog { get; set; }
}
