using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JiraWorkLogAssistant.Core.Services;

public class AIService : IAIService
{
    private readonly ISettingsService _settings;
    private readonly ILogger<AIService> _logger;

    public AIService(ISettingsService settings, ILogger<AIService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    private HttpClient CreateClient(AISettings s)
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        client.BaseAddress = new Uri(s.BaseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrEmpty(s.ApiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);
        return client;
    }

    public async Task<bool> TestConnectionAsync(AISettings settings)
    {
        try
        {
            using var client = CreateClient(settings);
            var payload = new Dictionary<string, object?>
            {
                ["messages"] = new[] { new { role = "user", content = "Say OK" } },
                ["max_tokens"] = 5,
                ["stream"] = false
            };
            // Only send "model" when provided; otherwise the endpoint URL itself selects the model.
            if (!string.IsNullOrWhiteSpace(settings.ModelName))
                payload["model"] = settings.ModelName;
            var resp = await client.PostAsync("chat/completions", CreateJson(payload));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) { _logger.LogError(ex, "AI test failed"); return false; }
    }

    public async Task<string> GenerateWorkLogAsync(IEnumerable<string> notes, string issueKey, string issueSummary)
    {
        var noteList = string.Join("\n", notes.Select((n, i) => $"- {n}"));
        var prompt = $"""
            Convert these raw work notes into a professional Jira work log entry (2-4 sentences, past tense, no bullet points):

            Issue: {issueKey} - {issueSummary}
            Notes:
            {noteList}

            Output only the work log text, nothing else.
            """;
        return await ChatAsync(prompt);
    }

    /// <summary>
    /// Polishes each note into its own concise professional point and returns them as Jira
    /// wiki-markup bullet lines (each prefixed with "* "). Used by Smart Distribution.
    /// </summary>
    public async Task<string> GenerateBulletedWorkLogAsync(IEnumerable<string> notes, string issueKey, string issueSummary)
    {
        var noteArr = notes.ToList();
        var noteList = string.Join("\n", noteArr.Select((n, i) => $"{i + 1}. {n}"));
        // Note: do NOT dictate length/style here — that is governed by the (configurable) system prompt,
        // so a user prompt asking for "2-3 sentences each" actually takes effect.
        var prompt = $$"""
            Turn each of the work notes below into a professional Jira work-log entry, following the
            style and length described in your system instructions. Use the issue context to add
            relevant, accurate detail. Keep exactly one entry per note, in the same order; do not merge,
            split, invent, or drop notes.

            Issue: {{issueKey}} - {{issueSummary}}
            Notes:
            {{noteList}}

            Return ONLY a JSON array of strings — one element per note, in order. Example:
            ["Investigated the authentication failure, traced it to an expired token, and documented the root cause.", "Implemented the validation fix and verified it against the failing scenarios."]
            """;

        // Use the user's custom system prompt if set; otherwise the built-in default.
        var aiSettings = await _settings.GetAISettingsAsync();
        var systemPrompt = string.IsNullOrWhiteSpace(aiSettings.WorkLogSystemPrompt)
            ? DefaultWorkLogSystemPrompt : aiSettings.WorkLogSystemPrompt;

        var raw = await ChatAsync(prompt, systemPrompt);

        // Parse the JSON array; fall back to splitting lines if the model didn't comply.
        var points = new List<string>();
        try
        {
            var start = raw.IndexOf('[');
            var end = raw.LastIndexOf(']');
            if (start >= 0 && end > start)
            {
                using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var v = el.GetString();
                    if (!string.IsNullOrWhiteSpace(v)) points.Add(v.Trim());
                }
            }
        }
        catch { /* fall through to line splitting */ }

        if (points.Count == 0)
        {
            points = raw.Replace("\r", "").Split('\n')
                .Select(l => l.TrimStart('*', '-', '•', '·', ' '))
                .Where(l => l.Length > 0).ToList();
        }

        // Render as Jira wiki bullets.
        return string.Join("\n", points.Select(p => "* " + p));
    }

    public async Task<string> ImproveWorkLogAsync(string existingText)
    {
        var prompt = $"Rewrite this work log more professionally and concisely (2-4 sentences, past tense):\n\n{existingText}\n\nOutput only the improved text.";
        return await ChatAsync(prompt);
    }

    public async Task<string> GenerateDailySummaryAsync(IEnumerable<WorkLog> workLogs)
    {
        var logs = string.Join("\n", workLogs.Select(w => $"- {w.JiraIssue?.Key}: {w.GeneratedText}"));
        var prompt = $"Generate a professional daily summary based on these work logs:\n\n{logs}\n\nOutput a concise paragraph suitable for a status update.";
        return await ChatAsync(prompt);
    }

    public async Task<string> GenerateWeeklySummaryAsync(IEnumerable<WorkLog> workLogs)
    {
        var logs = string.Join("\n", workLogs.Select(w => $"- {w.JiraIssue?.Key}: {w.GeneratedText}"));
        var prompt = $"Generate a professional weekly summary based on these work logs:\n\n{logs}\n\nOutput a concise paragraph highlighting key achievements.";
        return await ChatAsync(prompt);
    }

    public async Task<string> GenerateStandupAsync(IEnumerable<WorkLog> recentLogs, IEnumerable<string> ongoing)
    {
        var done = string.Join("\n", recentLogs.Select(w => $"- {w.JiraIssue?.Key}: {w.GeneratedText}"));
        var inProgress = string.Join("\n", ongoing.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => $"- {o}"));
        if (string.IsNullOrWhiteSpace(done) && string.IsNullOrWhiteSpace(inProgress))
            return "No work logged yet today. Log some work, then generate the standup.";

        var prompt = $"""
            You are an agile scrum assistant. Using the engineer's submitted work logs and any
            in-progress notes below, write a concise daily scrum stand-up.

            Completed / submitted work logs:
            {(string.IsNullOrWhiteSpace(done) ? "(none)" : done)}

            In-progress notes:
            {(string.IsNullOrWhiteSpace(inProgress) ? "(none)" : inProgress)}

            Infer sensible blockers and risks from the content (e.g. dependencies, failing tests,
            unclear requirements, pending reviews). If there are genuinely none, write "None".

            Output EXACTLY in this format, using short bullet points under each heading:

            Update:
            - ...
            Blockers:
            - ...
            Risks:
            - ...
            """;
        return await ChatAsync(prompt);
    }

    public async Task<List<SmartDistributionResult>> DistributeNotesAsync(IEnumerable<WorkNote> notes, IEnumerable<JiraIssue> issues, int? totalMinutes)
    {
        var noteList = notes.ToList();
        var issueList = issues.ToList();

        var notesText = string.Join("\n", noteList.Select((n, i) => $"{i + 1}. {n.Content}"));
        var issuesText = string.Join("\n", issueList.Select(i => $"- {i.Key}: {i.Summary}. {i.Description?.Substring(0, Math.Min(i.Description?.Length ?? 0, 200))}"));

        var prompt = $$"""
            Match these work notes to Jira issues. Return JSON array only.

            Notes:
            {{notesText}}

            Jira Issues:
            {{issuesText}}

            Total minutes available: {{totalMinutes?.ToString() ?? "unknown"}}

            Return JSON array:
            [
              {
                "issueKey": "TASK-123",
                "noteIndices": [1, 2],
                "confidence": 0.95,
                "estimatedMinutes": 120
              }
            ]

            Rules:
            - Each note goes to exactly one issue (best match)
            - confidence is 0.0 to 1.0
            - If totalMinutes given, estimatedMinutes must sum to totalMinutes
            - Only include issues that have at least one note
            """;

        var json = await ChatAsync(prompt);

        try
        {
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start < 0 || end < 0) return new();
            json = json[start..(end + 1)];

            var doc = JsonDocument.Parse(json);
            var results = new List<SmartDistributionResult>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var key = item.GetProperty("issueKey").GetString() ?? string.Empty;
                var issue = issueList.FirstOrDefault(i => i.Key == key);
                if (issue == null) continue;

                var indices = item.GetProperty("noteIndices").EnumerateArray().Select(n => n.GetInt32() - 1).ToList();
                var assignedNotes = indices.Where(i => i >= 0 && i < noteList.Count).Select(i => noteList[i]).ToList();
                var confidence = item.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.5;
                var minutes = item.TryGetProperty("estimatedMinutes", out var m) ? m.GetInt32() : 0;

                var workLog = await GenerateBulletedWorkLogAsync(assignedNotes.Select(n => n.Content), issue.Key, issue.Summary);

                results.Add(new SmartDistributionResult
                {
                    Issue = issue,
                    AssignedNotes = assignedNotes,
                    Confidence = confidence,
                    EstimatedMinutes = minutes,
                    GeneratedWorkLog = workLog
                });
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse smart distribution response");
            return new();
        }
    }

    private const string DefaultSystemPrompt =
        "You are a professional technical work log writer. Be concise, accurate and professional.";

    /// <summary>Built-in default system prompt for work-log polishing (used when the user leaves it blank).</summary>
    public const string DefaultWorkLogSystemPrompt =
        "You are an engineering work-log assistant. Convert a developer's raw, terse notes into a " +
        "clear, professional Jira work log written as separate points — one point per note, in the " +
        "same order. Each point: past tense, factual, concise (one or two sentence), specific about " +
        "what was done. Do not merge, split, invent, or drop notes. No preamble, no summary, no headings.";

    private async Task<string> ChatAsync(string userMessage, string? systemPrompt = null)
    {
        HttpResponseMessage resp;
        string json;
        try
        {
            var s = await _settings.GetAISettingsAsync();
            using var client = CreateClient(s);
            var payload = new Dictionary<string, object?>
            {
                ["messages"] = new[]
                {
                    new { role = "system", content = systemPrompt ?? DefaultSystemPrompt },
                    new { role = "user", content = userMessage }
                },
                ["max_tokens"] = 1000,
                ["temperature"] = 0.3,
                ["stream"] = false
            };
            // Only send "model" when provided; otherwise the endpoint URL itself selects the model.
            if (!string.IsNullOrWhiteSpace(s.ModelName))
                payload["model"] = s.ModelName;
            resp = await client.PostAsync("chat/completions", CreateJson(payload));
            json = await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            // Bad URL, DNS failure, connection refused, timeout, etc.
            _logger.LogError(ex, "AI request could not reach the endpoint");
            throw new AIRequestException("Could not reach the AI endpoint. Please check your AI settings.", ex);
        }

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("AI request failed: HTTP {Code} {Body}", (int)resp.StatusCode, json);
            throw new AIRequestException(
                $"AI request failed (HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}). Please check your AI settings.");
        }

        // Some gateways stream by default and ignore "stream": false, replying with SSE
        // (text/event-stream: "data: {chunk}" lines). Reassemble those into plain text.
        if (LooksLikeSse(resp, json))
        {
            var streamed = ParseSseContent(json);
            if (string.IsNullOrWhiteSpace(streamed))
                throw new AIRequestException("The AI endpoint returned an empty response. Please check your AI settings.");
            return streamed;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
                throw new AIRequestException("The AI endpoint returned an empty response. Please check your AI settings.");
            return content;
        }
        catch (AIRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI response was not in the expected format: {Body}", json);
            throw new AIRequestException("The AI endpoint returned an unexpected response. Please check your AI settings.", ex);
        }
    }

    private static StringContent CreateJson(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }

    private static bool LooksLikeSse(HttpResponseMessage resp, string body)
    {
        var ct = resp.Content.Headers.ContentType?.MediaType;
        if (string.Equals(ct, "text/event-stream", StringComparison.OrdinalIgnoreCase))
            return true;
        // Content-type may be wrong/missing; sniff the body.
        return body.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reassembles an OpenAI-style SSE stream ("data: {chunk}\n\n" lines, ending with "data: [DONE]")
    /// into the full assistant message by concatenating every choices[0].delta.content fragment.
    /// </summary>
    private static string ParseSseContent(string body)
    {
        var sb = new StringBuilder();
        foreach (var rawLine in body.Replace("\r", "").Split('\n'))
        {
            var line = rawLine.TrimStart();
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            var data = line.Substring(5).Trim();
            if (data.Length == 0 || data == "[DONE]") continue;

            try
            {
                using var doc = JsonDocument.Parse(data);
                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    continue;
                var choice = choices[0];
                // Streamed chunks use "delta"; a non-streamed object smuggled over SSE uses "message".
                if (choice.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("content", out var dc) && dc.ValueKind == JsonValueKind.String)
                    sb.Append(dc.GetString());
                else if (choice.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var mc) && mc.ValueKind == JsonValueKind.String)
                    sb.Append(mc.GetString());
            }
            catch { /* skip malformed chunk */ }
        }
        return sb.ToString().Trim();
    }
}

/// <summary>Thrown when an AI request fails (bad settings, unreachable endpoint, or bad response).</summary>
public class AIRequestException : Exception
{
    public AIRequestException(string message, Exception? inner = null) : base(message, inner) { }
}
