using JiraWorkLogAssistant.Core.Models;

namespace JiraWorkLogAssistant.Core.Services.Interfaces;

public interface ISettingsService
{
    Task<JiraSettings> GetJiraSettingsAsync();
    Task SaveJiraSettingsAsync(JiraSettings settings);
    Task<AISettings> GetAISettingsAsync();
    Task SaveAISettingsAsync(AISettings settings);
    Task<AutoLoggingSettings> GetAutoLoggingSettingsAsync();
    Task SaveAutoLoggingSettingsAsync(AutoLoggingSettings settings);
    Task<BitbucketSettings> GetBitbucketSettingsAsync();
    Task SaveBitbucketSettingsAsync(BitbucketSettings settings);
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, bool encrypt = false);
}
