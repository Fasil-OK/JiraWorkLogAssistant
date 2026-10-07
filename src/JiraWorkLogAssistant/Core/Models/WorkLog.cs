namespace JiraWorkLogAssistant.Core.Models;

public enum WorkLogStatus { Draft, PendingSubmission, Submitted, Failed }

public class WorkLog
{
    public int Id { get; set; }
    public int JiraIssueId { get; set; }
    public string GeneratedText { get; set; } = string.Empty;
    public int TimeSpentMinutes { get; set; }
    public DateTime WorkDate { get; set; } = DateTime.Today;
    public WorkLogStatus Status { get; set; } = WorkLogStatus.Draft;
    public string? JiraWorkLogId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsApproved { get; set; }

    public JiraIssue JiraIssue { get; set; } = null!;
    public ICollection<WorkNote> Notes { get; set; } = new List<WorkNote>();
}
