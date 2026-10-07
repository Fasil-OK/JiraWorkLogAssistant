using JiraWorkLogAssistant.Core.Data;
using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JiraWorkLogAssistant.Core.Services;

/// <summary>
/// Background worker that fires per-window auto-logging. Each sticky-note session that has
/// AutoLog enabled is submitted to Jira at its configured time, after which its notes are
/// archived and the window is closed (via <see cref="CloseWindowCallback"/>).
/// </summary>
public class AutoLoggingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INotificationService _notification;
    private readonly ILogger<AutoLoggingService> _logger;

    /// <summary>Set by WindowManager so the service can close a task window after auto-submission.</summary>
    public static Action<int>? CloseWindowCallback { get; set; }

    private DateTime _lastRunMinute = DateTime.MinValue;

    public event EventHandler? LogsSubmitted;

    public AutoLoggingService(IServiceScopeFactory scopeFactory, INotificationService notification,
        ILogger<AutoLoggingService> logger)
    {
        _scopeFactory = scopeFactory;
        _notification = notification;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
            catch (TaskCanceledException) { break; }

            try
            {
                var now = DateTime.Now;
                // Only evaluate once per clock-minute.
                var thisMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                if (thisMinute == _lastRunMinute) continue;
                _lastRunMinute = thisMinute;

                await RunDueAutoLogsAsync(now);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Auto logging tick failed");
            }
        }
    }

    private async Task RunDueAutoLogsAsync(DateTime now)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jira = scope.ServiceProvider.GetRequiredService<IJiraService>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();

        // Global auto-logging is configured in Settings.
        var cfg = await settings.GetAutoLoggingSettingsAsync();
        if (!cfg.EnableAutoLogging) return;
        var target = cfg.AutoLoggingTime;

        // Fire when the current minute matches (or has just passed) the configured time today.
        var nowTod = now.TimeOfDay;
        if (nowTod < target || (nowTod - target) > TimeSpan.FromMinutes(2)) return;

        var sessions = await db.StickyNoteSessions
            .Include(s => s.JiraIssue)
            .Where(s => s.IsOpen && s.JiraIssueId != null && s.SessionType == SessionType.TaskSpecific)
            .ToListAsync();

        int submitted = 0;
        foreach (var session in sessions)
        {
            var issue = session.JiraIssue!;

            // Auto-log submits the note's written content AS-IS (no AI polishing, no review).
            var content = session.DraftText?.Trim();
            if (string.IsNullOrWhiteSpace(content)) continue;

            var totalMinutes = session.OverallDurationMinutes ?? 0;
            if (totalMinutes == 0) totalMinutes = 30; // sensible default so the log is accepted

            // Stamp the worklog at today's configured auto-log time (e.g. 18:00), not midnight.
            var started = DateTime.Today + target;
            var ok = await jira.SubmitWorkLogAsync(issue.Key, content, totalMinutes, started);
            if (!ok) continue;

            db.WorkLogs.Add(new WorkLog
            {
                JiraIssueId = issue.Id,
                GeneratedText = content,
                TimeSpentMinutes = totalMinutes,
                WorkDate = DateTime.Today,
                Status = WorkLogStatus.Submitted,
                SubmittedAt = DateTime.UtcNow
            });
            // Clear the draft and close the window (matches manual post-submit behavior).
            session.DraftRtf = null;
            session.DraftText = null;
            session.IsOpen = false;
            await db.SaveChangesAsync();

            CloseWindowCallback?.Invoke(issue.Id);
            submitted++;
        }

        if (submitted > 0)
        {
            _notification.ShowSuccess("Auto Work Logs", $"Auto-logged {submitted} task(s) and cleared their notes.");
            LogsSubmitted?.Invoke(this, EventArgs.Empty);
        }
    }
}
