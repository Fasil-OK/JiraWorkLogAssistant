using JiraWorkLogAssistant.Core.Models;

namespace JiraWorkLogAssistant.Core.Services.Interfaces;

public interface IAIService
{
    Task<bool> TestConnectionAsync(AISettings settings);
    Task<string> GenerateWorkLogAsync(IEnumerable<string> notes, string issueKey, string issueSummary);
    Task<string> GenerateBulletedWorkLogAsync(IEnumerable<string> notes, string issueKey, string issueSummary);
    Task<string> ImproveWorkLogAsync(string existingText);
    Task<string> GenerateDailySummaryAsync(IEnumerable<WorkLog> workLogs);
    Task<string> GenerateWeeklySummaryAsync(IEnumerable<WorkLog> workLogs);
    Task<string> GenerateStandupAsync(IEnumerable<WorkLog> recentLogs, IEnumerable<string> ongoing);
    Task<List<SmartDistributionResult>> DistributeNotesAsync(IEnumerable<WorkNote> notes, IEnumerable<JiraIssue> issues, int? totalMinutes);
}
