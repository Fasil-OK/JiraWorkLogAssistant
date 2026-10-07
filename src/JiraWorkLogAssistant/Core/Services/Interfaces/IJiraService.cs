using JiraWorkLogAssistant.Core.Models;

namespace JiraWorkLogAssistant.Core.Services.Interfaces;

public interface IJiraService
{
    Task<bool> TestConnectionAsync(JiraSettings settings);
    Task<IEnumerable<JiraIssue>> FetchAssignedIssuesAsync();
    Task<JiraIssue?> GetIssueAsync(string key);
    Task<bool> SubmitWorkLogAsync(string issueKey, string comment, int timeSpentMinutes, DateTime started);
    Task SyncIssuesAsync();
}
