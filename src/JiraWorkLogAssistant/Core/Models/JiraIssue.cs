using System.Linq;

namespace JiraWorkLogAssistant.Core.Models;

public class JiraIssue
{
    /// <summary>Only these Jira issue types are shown in pickers and used in Smart Distribution.</summary>
    public static readonly string[] AllowedTypes = { "Task", "Sub-task", "Subtask", "Bug", "Improvement" };

    public static bool IsAllowedType(string? type)
        => type != null && AllowedTypes.Contains(type, StringComparer.OrdinalIgnoreCase);

    /// <summary>Dropdown/list dot color by issue type: Task/Sub-task = blue, Improvement = green, Bug = red.</summary>
    public static string ColorForType(string? type)
    {
        if (type == null) return "#64748B";
        if (type.Equals("Bug", StringComparison.OrdinalIgnoreCase)) return "#EF4444";
        if (type.Equals("Improvement", StringComparison.OrdinalIgnoreCase)) return "#22C55E";
        if (type.Equals("Task", StringComparison.OrdinalIgnoreCase)
            || type.Equals("Sub-task", StringComparison.OrdinalIgnoreCase)
            || type.Equals("Subtask", StringComparison.OrdinalIgnoreCase)) return "#3B82F6";
        return "#64748B";
    }


    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Assignee { get; set; }
    public string? Priority { get; set; }
    public string? IssueType { get; set; }
    public string? Project { get; set; }
    public string? SprintName { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string BorderColor { get; set; } = "#2196F3";
    public bool IsActive { get; set; } = true;

    /// <summary>Type-based color for dropdown/list dots (not stored).</summary>
    public string TypeColor => ColorForType(IssueType);
    /// <summary>"KEY — Summary" for tooltips.</summary>
    public string KeyAndSummary => $"{Key} — {Summary}";

    public ICollection<WorkNote> Notes { get; set; } = new List<WorkNote>();
    public ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();
    public ICollection<ChecklistItem> ChecklistItems { get; set; } = new List<ChecklistItem>();
    public ICollection<StickyNoteSession> Sessions { get; set; } = new List<StickyNoteSession>();
}
