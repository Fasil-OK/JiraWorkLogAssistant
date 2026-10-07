using JiraWorkLogAssistant.Core.Data;
using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace JiraWorkLogAssistant.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;
    private readonly EncryptionService _enc;

    public SettingsService(AppDbContext db, EncryptionService enc)
    {
        _db = db;
        _enc = enc;
    }

    public async Task<string?> GetAsync(string key)
    {
        var setting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null) return null;
        return setting.IsEncrypted ? _enc.Decrypt(setting.Value) : setting.Value;
    }

    public async Task SetAsync(string key, string value, bool encrypt = false)
    {
        var stored = encrypt ? _enc.Encrypt(value) : value;
        var existing = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing == null)
        {
            _db.Settings.Add(new AppSettings { Key = key, Value = stored, IsEncrypted = encrypt, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            existing.Value = stored;
            existing.IsEncrypted = encrypt;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<JiraSettings> GetJiraSettingsAsync()
    {
        return new JiraSettings
        {
            BaseUrl = await GetAsync("jira.baseUrl") ?? string.Empty,
            Username = await GetAsync("jira.username") ?? string.Empty,
            Password = await GetAsync("jira.password") ?? string.Empty
        };
    }

    public async Task SaveJiraSettingsAsync(JiraSettings s)
    {
        await SetAsync("jira.baseUrl", s.BaseUrl);
        await SetAsync("jira.username", s.Username);
        await SetAsync("jira.password", s.Password, true);
    }

    public async Task<AISettings> GetAISettingsAsync()
    {
        return new AISettings
        {
            BaseUrl = await GetAsync("ai.baseUrl") ?? string.Empty,
            ApiKey = await GetAsync("ai.apiKey") ?? string.Empty,
            ModelName = await GetAsync("ai.modelName") ?? string.Empty,
            WorkLogSystemPrompt = await GetAsync("ai.workLogPrompt") ?? string.Empty,
            EnablePolishing = (await GetAsync("ai.enablePolishing") ?? "true") != "false"
        };
    }

    public async Task SaveAISettingsAsync(AISettings s)
    {
        await SetAsync("ai.baseUrl", s.BaseUrl);
        await SetAsync("ai.apiKey", s.ApiKey, true);
        await SetAsync("ai.modelName", s.ModelName);
        await SetAsync("ai.workLogPrompt", s.WorkLogSystemPrompt);
        await SetAsync("ai.enablePolishing", s.EnablePolishing ? "true" : "false");
    }

    public async Task<AutoLoggingSettings> GetAutoLoggingSettingsAsync()
    {
        var json = await GetAsync("autoLogging.settings");
        if (json == null) return new AutoLoggingSettings();
        try { return JsonSerializer.Deserialize<AutoLoggingSettings>(json) ?? new AutoLoggingSettings(); }
        catch { return new AutoLoggingSettings(); }
    }

    public async Task SaveAutoLoggingSettingsAsync(AutoLoggingSettings s)
    {
        var json = JsonSerializer.Serialize(s);
        await SetAsync("autoLogging.settings", json);
    }

    public async Task<BitbucketSettings> GetBitbucketSettingsAsync()
    {
        var urls = (await GetAsync("bitbucket.repoUrls") ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim()).Where(u => u.Length > 0).ToList();
        return new BitbucketSettings
        {
            Username = await GetAsync("bitbucket.username") ?? string.Empty,
            Password = await GetAsync("bitbucket.password") ?? string.Empty,
            RepoUrls = urls
        };
    }

    public async Task SaveBitbucketSettingsAsync(BitbucketSettings s)
    {
        await SetAsync("bitbucket.username", s.Username);
        await SetAsync("bitbucket.password", s.Password, true);
        await SetAsync("bitbucket.repoUrls", string.Join("\n", s.RepoUrls));
    }
}
