using CommunityToolkit.Mvvm.Input;
using JiraWorkLogAssistant.Core.Data;
using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.Core.Services;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using JiraWorkLogAssistant.ViewModels.Base;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace JiraWorkLogAssistant.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private readonly IAIService _aiService;

    private int _todayTaskCount;
    public int TodayTaskCount { get => _todayTaskCount; set => SetProperty(ref _todayTaskCount, value); }

    private string _todayHoursLogged = "0h";
    public string TodayHoursLogged { get => _todayHoursLogged; set => SetProperty(ref _todayHoursLogged, value); }

    private int _pendingWorkLogs;
    public int PendingWorkLogs { get => _pendingWorkLogs; set => SetProperty(ref _pendingWorkLogs, value); }

    private string _dailySummary = string.Empty;
    public string DailySummary { get => _dailySummary; set => SetProperty(ref _dailySummary, value); }

    private string _standupText = string.Empty;
    public string StandupText { get => _standupText; set => SetProperty(ref _standupText, value); }

    public ObservableCollection<WorkLog> RecentWorkLogs { get; } = new();
    public ObservableCollection<JiraIssue> ActiveIssues { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand GenerateDailySummaryCommand { get; }
    public IAsyncRelayCommand GenerateStandupCommand { get; }

    public DashboardViewModel(AppDbContext db, IAIService ai)
    {
        _db = db;
        _aiService = ai;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        GenerateDailySummaryCommand = new AsyncRelayCommand(GenerateDailySummaryAsync);
        GenerateStandupCommand = new AsyncRelayCommand(GenerateStandupAsync);
    }

    public async Task LoadAsync()
    {
        var today = DateTime.Today;

        var todayLogs = await _db.WorkLogs
            .Include(w => w.JiraIssue)
            .Where(w => w.WorkDate == today)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        TodayTaskCount = todayLogs.Select(w => w.JiraIssueId).Distinct().Count();

        var totalMin = todayLogs.Sum(w => w.TimeSpentMinutes);
        TodayHoursLogged = totalMin > 0 ? $"{totalMin / 60}h {totalMin % 60}m" : "0h";

        PendingWorkLogs = await _db.WorkNotes
            .CountAsync(n => n.Status == NoteStatus.Active);

        RecentWorkLogs.Clear();
        foreach (var log in todayLogs.Take(10)) RecentWorkLogs.Add(log);

        var active = await _db.JiraIssues
            .Where(i => i.Notes.Any(n => n.Status == NoteStatus.Active))
            .Take(10).ToListAsync();
        ActiveIssues.Clear();
        foreach (var i in active) ActiveIssues.Add(i);
    }

    private async Task GenerateDailySummaryAsync()
    {
        await RunBusyAsync(async () =>
        {
            try
            {
                var today = DateTime.Today;
                var logs = await _db.WorkLogs.Include(w => w.JiraIssue).Where(w => w.WorkDate == today).ToListAsync();
                DailySummary = await _aiService.GenerateDailySummaryAsync(logs);
            }
            catch (AIRequestException ex) { DailySummary = "⚠ " + ex.Message; }
        }, "Generating summary...");
    }

    private async Task GenerateStandupAsync()
    {
        await RunBusyAsync(async () =>
        {
            // Recent submitted work logs (today + yesterday) = "what was done".
            var since = DateTime.Today.AddDays(-1);
            var recentLogs = await _db.WorkLogs.Include(w => w.JiraIssue)
                .Where(w => w.WorkDate >= since)
                .OrderByDescending(w => w.WorkDate).ToListAsync();

            // Open session drafts = "in progress" work for blockers/risks inference.
            var ongoing = await _db.StickyNoteSessions
                .Where(s => s.IsOpen && s.DraftText != null && s.DraftText != "")
                .Select(s => s.DraftText!).ToListAsync();

            try { StandupText = await _aiService.GenerateStandupAsync(recentLogs, ongoing); }
            catch (AIRequestException ex) { StandupText = "⚠ " + ex.Message; }
        }, "Generating standup...");
    }
}
