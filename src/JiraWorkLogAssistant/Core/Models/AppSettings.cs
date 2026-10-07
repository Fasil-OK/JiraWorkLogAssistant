namespace JiraWorkLogAssistant.Core.Models;

public class AppSettings
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class JiraSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class BitbucketSettings
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    /// <summary>Full repository browse URLs, e.g. http://host/bitbucket/projects/INDIA/repos/repo_name</summary>
    public List<string> RepoUrls { get; set; } = new();
}

/// <summary>A commit fetched from Bitbucket.</summary>
public class BitbucketCommit
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string? JiraKey { get; set; }
    public DateTime AuthoredAt { get; set; }
    /// <summary>First line of the commit message.</summary>
    public string Subject => Message.Split('\n')[0].Trim();
}

public class AISettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Custom system prompt for work-log polishing. Blank = use the built-in default.</summary>
    public string WorkLogSystemPrompt { get; set; } = string.Empty;

    /// <summary>When on, notes are AI-polished and reviewed before submit. Off = log raw text directly.</summary>
    public bool EnablePolishing { get; set; } = true;
}

public class AutoLoggingSettings
{
    public bool EnableAutoLogging { get; set; }
    public TimeSpan AutoLoggingTime { get; set; } = new TimeSpan(18, 0, 0);
    public string Timezone { get; set; } = TimeZoneInfo.Local.Id;
    public bool RequireApprovalBeforeSubmission { get; set; } = true;
    public bool EnableDailySummary { get; set; } = true;
    public bool EnableSmartDistribution { get; set; } = true;
    public bool ArchiveNotesAfterSubmission { get; set; } = true;
    public bool CloseWindowsAfterSubmission { get; set; } = true;
    public bool LaunchOnStartup { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool ShowIdleNotification { get; set; } = true;
    public int IdleNotificationMinutes { get; set; } = 120;
    public double ConfidenceThreshold { get; set; } = 0.70;
}

public class SmartDistributionResult
{
    public JiraIssue Issue { get; set; } = null!;
    public List<WorkNote> AssignedNotes { get; set; } = new();
    public double Confidence { get; set; }
    public int EstimatedMinutes { get; set; }
    public string GeneratedWorkLog { get; set; } = string.Empty;
}
