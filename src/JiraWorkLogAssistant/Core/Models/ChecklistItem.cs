namespace JiraWorkLogAssistant.Core.Models;

public class ChecklistItem
{
    public int Id { get; set; }
    public int JiraIssueId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int? DurationMinutes { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public JiraIssue JiraIssue { get; set; } = null!;
}
