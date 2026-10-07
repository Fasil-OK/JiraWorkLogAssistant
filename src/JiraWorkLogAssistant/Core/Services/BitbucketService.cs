using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JiraWorkLogAssistant.Core.Services;

public class BitbucketService : IBitbucketService
{
    private readonly ISettingsService _settings;
    private readonly ILogger<BitbucketService> _logger;

    private static readonly Regex UrlPattern = new(
        @"^(?<root>https?://.+?)/projects/(?<proj>[^/]+)/repos?/(?<repo>[^/?#]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex JiraKeyPattern = new(@"[A-Z][A-Z0-9]+-\d+", RegexOptions.Compiled);

    public BitbucketService(ISettingsService settings, ILogger<BitbucketService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Parse a repo browse URL into its REST base, project key and repo slug.</summary>
    private static (string RestRoot, string Project, string Repo)? Parse(string url)
    {
        var m = UrlPattern.Match(url.Trim());
        if (!m.Success) return null;
        return (m.Groups["root"].Value.TrimEnd('/'), m.Groups["proj"].Value, m.Groups["repo"].Value);
    }

    private static HttpClient CreateClient(BitbucketSettings s)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{s.Username}:{s.Password}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    public async Task<bool> TestConnectionAsync(BitbucketSettings settings)
    {
        var first = settings.RepoUrls.Select(Parse).FirstOrDefault(p => p != null);
        if (first == null) return false;
        try
        {
            using var http = CreateClient(settings);
            var (root, proj, repo) = first.Value;
            var resp = await http.GetAsync($"{root}/rest/api/1.0/projects/{proj}/repos/{repo}");
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) { _logger.LogError(ex, "Bitbucket test failed"); return false; }
    }

    public async Task<List<BitbucketCommit>> GetCommitsAsync(string? taskKey, DateTime date)
    {
        var settings = await _settings.GetBitbucketSettingsAsync();
        var result = new List<BitbucketCommit>();
        if (!settings.RepoUrls.Any()) return result;

        using var http = CreateClient(settings);
        var day = date.Date;
        var seen = new HashSet<string>();

        foreach (var url in settings.RepoUrls)
        {
            var parsed = Parse(url);
            if (parsed == null) { _logger.LogWarning("Unrecognized Bitbucket repo URL: {Url}", url); continue; }
            var (root, proj, repo) = parsed.Value;
            var api = $"{root}/rest/api/1.0/projects/{proj}/repos/{repo}";
            var repoName = repo;

            try
            {
                // 1) Which branches to inspect.
                var branchQuery = taskKey != null
                    ? $"{api}/branches?filterText={Uri.EscapeDataString(taskKey)}&limit=50"
                    : $"{api}/branches?orderBy=MODIFICATION&limit=40";
                var branches = await GetBranchesAsync(http, branchQuery);

                foreach (var (branchId, branchName) in branches)
                {
                    var commitsJson = await GetJsonAsync(http, $"{api}/commits?until={Uri.EscapeDataString(branchId)}&limit=50");
                    if (commitsJson == null) continue;

                    foreach (var c in commitsJson.RootElement.GetProperty("values").EnumerateArray())
                    {
                        var id = c.GetProperty("id").GetString() ?? "";
                        if (seen.Contains(id)) continue;

                        var tsMs = c.TryGetProperty("authorTimestamp", out var tp) ? tp.GetInt64() : 0;
                        var authored = DateTimeOffset.FromUnixTimeMilliseconds(tsMs).LocalDateTime;
                        // Commits come newest-first; stop scanning this branch once we pass the target day.
                        if (authored.Date < day) break;
                        if (authored.Date != day) continue;

                        if (!AuthorMatches(c, settings.Username)) continue;

                        var msg = c.TryGetProperty("message", out var mp) ? mp.GetString() ?? "" : "";
                        seen.Add(id);
                        result.Add(new BitbucketCommit
                        {
                            Id = id,
                            Message = msg.Trim(),
                            Branch = branchName,
                            RepoName = repoName,
                            AuthoredAt = authored,
                            JiraKey = ExtractKey(branchName) ?? ExtractKey(msg)
                        });
                    }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Fetching commits from {Repo} failed", repoName); }
        }

        return result.OrderBy(c => c.AuthoredAt).ToList();
    }

    private async Task<List<(string Id, string Name)>> GetBranchesAsync(HttpClient http, string query)
    {
        var list = new List<(string, string)>();
        var doc = await GetJsonAsync(http, query);
        if (doc == null) return list;
        foreach (var b in doc.RootElement.GetProperty("values").EnumerateArray())
        {
            var id = b.TryGetProperty("id", out var ip) ? ip.GetString() ?? "" : "";
            var name = b.TryGetProperty("displayId", out var dp) ? dp.GetString() ?? id : id;
            if (id.Length > 0) list.Add((id, name));
        }
        return list;
    }

    private static bool AuthorMatches(JsonElement commit, string username)
    {
        if (!commit.TryGetProperty("author", out var a)) return false;
        var name = a.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
        var email = a.TryGetProperty("emailAddress", out var ep) ? ep.GetString() ?? "" : "";
        return name.Equals(username, StringComparison.OrdinalIgnoreCase)
            || email.Equals(username, StringComparison.OrdinalIgnoreCase)
            || email.StartsWith(username + "@", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractKey(string text)
    {
        var m = JiraKeyPattern.Match(text ?? "");
        return m.Success ? m.Value.ToUpperInvariant() : null;
    }

    private async Task<JsonDocument?> GetJsonAsync(HttpClient http, string url)
    {
        try
        {
            var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            return JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        }
        catch { return null; }
    }
}
