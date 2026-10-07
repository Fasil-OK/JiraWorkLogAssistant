using JiraWorkLogAssistant.Core.Models;

namespace JiraWorkLogAssistant.Core.Services.Interfaces;

public interface IBitbucketService
{
    Task<bool> TestConnectionAsync(BitbucketSettings settings);

    /// <summary>
    /// Commits authored by the configured user on the given date across the configured repos.
    /// When <paramref name="taskKey"/> is set, only branches whose name contains that key are searched.
    /// </summary>
    Task<List<BitbucketCommit>> GetCommitsAsync(string? taskKey, DateTime date);
}
