using JiraWorkLogAssistant.Core.Data;
using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JiraWorkLogAssistant.Core.Services;

public class JiraService : IJiraService
{
    private readonly AppDbContext _db;
    private readonly ISettingsService _settings;
    private readonly ILogger<JiraService> _logger;

    private static readonly string[] IssueColors = new[]
    {
        "#2196F3", "#4CAF50", "#FF9800", "#F44336", "#9C27B0",
        "#00BCD4", "#795548", "#607D8B", "#E91E63", "#3F51B5"
    };

    public JiraService(AppDbContext db, ISettingsService settings, ILogger<JiraService> logger)
    {
        _db = db;
        _settings = settings;
        _logger = logger;
    }

    // A fresh HttpClient per call: BaseAddress/headers can only be set before a client's first request,
    // so reusing one instance throws "this instance already started one or more requests".
    private async Task<HttpClient> CreateClientAsync(JiraSettings? overrideSettings = null)
    {
        var s = overrideSettings ?? await _settings.GetJiraSettingsAsync();
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{s.Username}:{s.Password}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.BaseAddress = new Uri(s.BaseUrl.TrimEnd('/') + "/");
        return http;
    }

    public async Task<bool> TestConnectionAsync(JiraSettings settings)
    {
        try
        {
            using var http = await CreateClientAsync(settings);
            var resp = await http.GetAsync("rest/api/2/myself");
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jira connection test failed");
            return false;
        }
    }

    public async Task<IEnumerable<JiraIssue>> FetchAssignedIssuesAsync()
    {
        try
        {
            using var http = await CreateClientAsync();
            var jql = Uri.EscapeDataString("assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC");
            var resp = await http.GetAsync($"rest/api/2/search?jql={jql}&maxResults=100&fields=summary,description,status,assignee,priority,issuetype,project");
            if (!resp.IsSuccessStatusCode) return Enumerable.Empty<JiraIssue>();

            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var issues = new List<JiraIssue>();

            foreach (var item in doc.RootElement.GetProperty("issues").EnumerateArray())
            {
                var key = item.GetProperty("key").GetString() ?? string.Empty;
                var fields = item.GetProperty("fields");
                var issueType = fields.TryGetProperty("issuetype", out var it) && it.TryGetProperty("name", out var itn) ? itn.GetString() : null;

                // Only Task / Sub-task / Bug / Improvement; skip SRS, CRS and other types.
                if (!JiraIssue.IsAllowedType(issueType))
                    continue;

                issues.Add(new JiraIssue
                {
                    Key = key,
                    Summary = fields.TryGetProperty("summary", out var s) ? s.GetString() ?? string.Empty : string.Empty,
                    Description = ExtractDescription(fields),
                    Status = fields.TryGetProperty("status", out var st) && st.TryGetProperty("name", out var stn) ? stn.GetString() : null,
                    IssueType = issueType,
                    LastSyncedAt = DateTime.UtcNow
                });
            }
            return issues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Jira issues");
            return Enumerable.Empty<JiraIssue>();
        }
    }

    private string? ExtractDescription(JsonElement fields)
    {
        if (!fields.TryGetProperty("description", out var desc)) return null;
        if (desc.ValueKind == JsonValueKind.Null) return null;
        // Jira Server/DC (v2) returns description as a plain string.
        if (desc.ValueKind == JsonValueKind.String)
            return desc.GetString();
        // Jira Cloud returns Atlassian Document Format (object) - extract plain text.
        try
        {
            var sb = new StringBuilder();
            ExtractTextFromAdf(desc, sb);
            return sb.ToString().Trim();
        }
        catch { return null; }
    }

    private void ExtractTextFromAdf(JsonElement node, StringBuilder sb)
    {
        if (node.TryGetProperty("text", out var text))
            sb.Append(text.GetString()).Append(' ');
        if (node.TryGetProperty("content", out var content))
            foreach (var child in content.EnumerateArray())
                ExtractTextFromAdf(child, sb);
    }

    public async Task<JiraIssue?> GetIssueAsync(string key)
    {
        return await _db.JiraIssues.FirstOrDefaultAsync(i => i.Key == key);
    }

    public async Task<bool> SubmitWorkLogAsync(string issueKey, string comment, int timeSpentMinutes, DateTime started)
    {
        try
        {
            using var http = await CreateClientAsync();
            // Jira Server/DC (v2): comment is a plain string that Jira renders as wiki markup.
            // 'started' must use a timezone offset WITHOUT a colon (e.g. +0530), or Jira rejects it.
            var startedStr = started.ToString("yyyy-MM-ddTHH:mm:ss.fff") + started.ToString("zzz").Replace(":", "");
            var body = JsonSerializer.Serialize(new
            {
                comment,
                timeSpentSeconds = timeSpentMinutes * 60,
                started = startedStr
            });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await http.PostAsync($"rest/api/2/issue/{issueKey}/worklog", content);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                _logger.LogError("Failed to submit work log to {Key}: {Error}", issueKey, err);
            }
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception submitting work log to {Key}", issueKey);
            return false;
        }
    }

    public async Task SyncIssuesAsync()
    {
        var remoteIssues = (await FetchAssignedIssuesAsync()).ToList();
        if (!remoteIssues.Any()) return; // treat empty as a transient failure; don't wipe existing

        var remoteKeys = remoteIssues.Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Deactivate anything no longer returned (e.g. previously-synced CRS/SRS, or now-done issues).
        var stale = await _db.JiraIssues.Where(i => i.IsActive && !remoteKeys.Contains(i.Key)).ToListAsync();
        foreach (var s in stale) s.IsActive = false;

        int colorIndex = 0;
        foreach (var remote in remoteIssues)
        {
            var existing = await _db.JiraIssues.FirstOrDefaultAsync(i => i.Key == remote.Key);
            if (existing == null)
            {
                remote.BorderColor = IssueColors[colorIndex % IssueColors.Length];
                remote.IsActive = true;
                colorIndex++;
                _db.JiraIssues.Add(remote);
            }
            else
            {
                existing.Summary = remote.Summary;
                existing.Description = remote.Description;
                existing.Status = remote.Status;
                existing.IssueType = remote.IssueType;
                existing.IsActive = true;
                existing.LastSyncedAt = DateTime.UtcNow;
                colorIndex++;
            }
        }
        await _db.SaveChangesAsync();
    }
}
