using CommunityToolkit.Mvvm.Input;
using JiraWorkLogAssistant.Core.Data;
using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.Core.Services;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using JiraWorkLogAssistant.ViewModels.Base;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace JiraWorkLogAssistant.ViewModels;

/// <summary>
/// Unified sticky-note style capture window. One writing panel; a dropdown of today's tasks;
/// a single Smart-mode toggle. Works in two modes:
///  - Manual: the panel's text is logged against the selected Jira task.
///  - Smart : the panel's lines are AI-distributed across today's in-progress tasks.
/// </summary>
public class NoteViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private readonly IAIService _aiService;
    private readonly IJiraService _jiraService;
    private readonly INotificationService _notification;
    private readonly ISettingsService _settings;
    private readonly IBitbucketService _bitbucket;
    private readonly NoteCoordinator _coordinator;

    // The distinct sticky-note color assigned to THIS window for its whole lifetime.
    private readonly string _windowColor;

    /// <summary>All of today's tasks (master list).</summary>
    public ObservableCollection<JiraIssue> TodaysTasks { get; } = new();

    /// <summary>Tasks the dropdown actually offers: today's tasks minus those open in other windows.</summary>
    public ObservableCollection<JiraIssue> AvailableTasks { get; } = new();

    /// <summary>Available tasks further narrowed by the search box text.</summary>
    public ObservableCollection<JiraIssue> FilteredTasks { get; } = new();

    private string _taskSearch = string.Empty;
    public string TaskSearch
    {
        get => _taskSearch;
        set { if (SetProperty(ref _taskSearch, value)) RebuildFilteredTasks(); }
    }

    /// <summary>True once at least one task has been synced (controls picker vs. sync banner).</summary>
    public bool HasTasks => TodaysTasks.Count > 0;

    private StickyNoteSession? _currentSession;
    private bool _suppressSwitch;

    public StickyNoteSession? Session => _currentSession;

    /// <summary>The window supplies the current RTF; the VM pushes RTF back when switching sessions.</summary>
    public Func<string?>? GetRtf { get; set; }
    public Action<string?>? SetRtf { get; set; }

    /// <summary>Raised when the active session changes so the window can recolor its border.</summary>
    public event EventHandler? AppearanceChanged;

    private JiraIssue? _selectedTask;
    public JiraIssue? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (_selectedTask == value) return;

            // Claim the new task; if another window already owns it, reject and revert.
            if (value != null && !_coordinator.TryClaimTask(value.Id))
            {
                StatusMessage = $"{value.Key} is already open in another note.";
                OnPropertyChanged(nameof(SelectedTask)); // snap ComboBox back
                return;
            }

            var previous = _selectedTask;
            _selectedTask = value;
            if (previous != null) _coordinator.ReleaseTask(previous.Id);
            OnPropertyChanged(nameof(SelectedTask));
            _ = OnSessionChangedAsync();
        }
    }

    private bool _smartMode;
    public bool SmartMode
    {
        get => _smartMode;
        set
        {
            if (SetProperty(ref _smartMode, value))
            {
                OnPropertyChanged(nameof(IsManualMode));
                OnPropertyChanged(nameof(ShowLogNow));
                _ = OnSessionChangedAsync();
            }
        }
    }

    public bool IsManualMode => !_smartMode;

    private bool _jiraLinked = true;
    public bool JiraLinked
    {
        get => _jiraLinked;
        set { SetProperty(ref _jiraLinked, value); OnPropertyChanged(nameof(ShowSyncPrompt)); }
    }

    public bool ShowSyncPrompt => !_jiraLinked || TodaysTasks.Count == 0;

    private int _hours;
    public int Hours
    {
        get => _hours;
        set { if (SetProperty(ref _hours, value)) PersistSessionMeta(); }
    }

    private int _minutes;
    public int Minutes
    {
        get => _minutes;
        set { if (SetProperty(ref _minutes, value)) PersistSessionMeta(); }
    }

    public int TotalMinutesInput => _hours * 60 + _minutes;

    // The date the work is logged against (manual only; defaults to today). Auto-log always uses today.
    private DateTime _selectedDate = DateTime.Today;
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set { if (SetProperty(ref _selectedDate, value)) OnPropertyChanged(nameof(SelectedDateLabel)); }
    }
    public string SelectedDateLabel => _selectedDate.ToString("dd-MM-yyyy");

    // Footer status color — red for errors, amber otherwise.
    private bool _statusIsError;
    public bool StatusIsError
    {
        get => _statusIsError;
        set { if (SetProperty(ref _statusIsError, value)) OnPropertyChanged(nameof(StatusColor)); }
    }
    public string StatusColor => _statusIsError ? "#EF4444" : "#B45309";

    // Global auto-logging (configured in Settings). When on, manual-mode logging is automatic,
    // so the "Log now" button is hidden. Smart mode always needs review, so it keeps the button.
    private bool _globalAutoLog;
    public bool GlobalAutoLog
    {
        get => _globalAutoLog;
        set { if (SetProperty(ref _globalAutoLog, value)) OnPropertyChanged(nameof(ShowLogNow)); }
    }

    /// <summary>The manual "Log now" button is shown unless global auto-logging is handling tasks.</summary>
    public bool ShowLogNow => _smartMode || !_globalAutoLog;

    /// <summary>Raised when a review dialog should open before submitting (Smart, or Manual with auto-log off).</summary>
    public event EventHandler<ReviewRequest>? ReviewRequested;

    // Smart mode keeps the purple AI accent; otherwise each window shows its own sticky color.
    public string CurrentBorderColor => _smartMode ? "#A855F7" : _windowColor;

    /// <summary>The window supplies this to append fetched commit points into the editor.</summary>
    public Action<IEnumerable<string>>? AppendPoints { get; set; }

    public IAsyncRelayCommand RefreshTasksCommand { get; }
    public IAsyncRelayCommand LogNowCommand { get; }
    public IAsyncRelayCommand FetchCommitsCommand { get; }

    public NoteViewModel(AppDbContext db, IAIService ai, IJiraService jira,
        INotificationService notification, ISettingsService settings, IBitbucketService bitbucket,
        NoteCoordinator coordinator)
    {
        _db = db;
        _aiService = ai;
        _jiraService = jira;
        _notification = notification;
        _settings = settings;
        _bitbucket = bitbucket;
        _coordinator = coordinator;
        _windowColor = coordinator.AcquireColor();
        _coordinator.TasksInUseChanged += OnTasksInUseChanged;
        _coordinator.SettingsChanged += OnSettingsChanged;
        RefreshTasksCommand = new AsyncRelayCommand(RefreshTasksAsync);
        LogNowCommand = new AsyncRelayCommand(LogNowAsync);
        FetchCommitsCommand = new AsyncRelayCommand(FetchCommitsAsync);
    }

    /// <summary>Fetch today's Bitbucket commits, summarize, and append as points into the writing box.</summary>
    private async Task FetchCommitsAsync()
    {
        var bb = await _settings.GetBitbucketSettingsAsync();
        if (!bb.RepoUrls.Any()) { SetError("Bitbucket not configured. Add repository URLs in Settings → Bitbucket."); return; }
        // Manual mode needs a task; Smart mode fetches across all of the user's tasks.
        if (!_smartMode && _selectedTask == null) { SetError("Please choose a task first before fetching commits."); return; }

        var ai = await _settings.GetAISettingsAsync();
        bool aiOk = ai.EnablePolishing && !string.IsNullOrWhiteSpace(ai.BaseUrl);

        var points = new List<string>();
        string? error = null;
        await RunBusyAsync(async () =>
        {
            var key = _smartMode ? null : _selectedTask!.Key;
            var commits = await _bitbucket.GetCommitsAsync(key, _selectedDate.Date);
            if (!commits.Any()) { error = $"No commits found for {(key ?? "your tasks")} on {_selectedDate:dd-MM-yyyy}."; return; }

            if (_smartMode)
            {
                // Group by detected Jira key; summarize each group, prefix points with the key.
                foreach (var grp in commits.GroupBy(c => c.JiraKey ?? "UNLINKED"))
                {
                    var summary = await SummarizePointsAsync(grp.Select(c => c.Message), grp.Key, "", aiOk);
                    var prefix = grp.Key == "UNLINKED" ? "" : grp.Key + ": ";
                    points.AddRange(summary.Select(p => prefix + p));
                }
            }
            else
            {
                points.AddRange(await SummarizePointsAsync(commits.Select(c => c.Message),
                    _selectedTask!.Key, _selectedTask!.Summary, aiOk));
            }
        }, "Fetching commits from Bitbucket…");

        if (error != null) { SetError(error); return; }
        if (points.Any())
        {
            AppendPoints?.Invoke(points);
            SetInfo($"Added {points.Count} point(s) from today's commits.");
        }
    }

    private async Task<List<string>> SummarizePointsAsync(IEnumerable<string> messages, string key, string summary, bool aiOk)
    {
        var msgs = messages.Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
        if (!msgs.Any()) return new();
        if (aiOk)
        {
            try
            {
                var text = await _aiService.GenerateBulletedWorkLogAsync(msgs, key, summary);
                var lines = text.Replace("\r", "").Split('\n')
                    .Select(l => l.TrimStart('*', '-', '•', ' ')).Where(l => l.Length > 0).ToList();
                if (lines.Any()) return lines;
            }
            catch (AIRequestException) { /* fall back to raw subjects */ }
        }
        // Raw fallback: first line of each commit message.
        return msgs.Select(m => m.Split('\n')[0].Trim()).Where(l => l.Length > 0).ToList();
    }

    private void OnTasksInUseChanged()
    {
        var app = System.Windows.Application.Current;
        if (app != null) app.Dispatcher.Invoke(RebuildAvailableTasks);
        else RebuildAvailableTasks();
    }

    private void OnSettingsChanged()
    {
        // Read the freshly-set value from the shared coordinator (not the scoped DbContext, which may be stale).
        var app = System.Windows.Application.Current;
        if (app != null) app.Dispatcher.Invoke(() => GlobalAutoLog = _coordinator.AutoLogEnabled);
        else GlobalAutoLog = _coordinator.AutoLogEnabled;
    }

    private void RebuildAvailableTasks()
    {
        AvailableTasks.Clear();
        foreach (var t in TodaysTasks)
        {
            // Offer a task only if no other window has claimed it (or it's the one we hold).
            if (!_coordinator.IsTaskInUse(t.Id) || (_selectedTask != null && t.Id == _selectedTask.Id))
                AvailableTasks.Add(t);
        }
        RebuildFilteredTasks();
    }

    private void RebuildFilteredTasks()
    {
        var s = _taskSearch?.Trim().ToLowerInvariant() ?? string.Empty;
        FilteredTasks.Clear();
        foreach (var t in AvailableTasks)
        {
            if (s.Length == 0
                || t.Key.ToLowerInvariant().Contains(s)
                || t.Summary.ToLowerInvariant().Contains(s))
            {
                FilteredTasks.Add(t);
            }
        }
    }

    public JiraIssue? CurrentTask => _selectedTask;

    /// <summary>Called when this window's task was auto-logged in the background: clear the panel.</summary>
    public void ResetAfterAutoLog()
    {
        if (_currentSession != null) { _currentSession.DraftRtf = null; _currentSession.DraftText = null; }
        SetRtf?.Invoke(null);
        Hours = 0; Minutes = 0;
        StatusMessage = "✓ Auto-logged to Jira and cleared.";
    }

    /// <summary>Called when the window closes: free this window's task and color.</summary>
    public void Cleanup()
    {
        _coordinator.TasksInUseChanged -= OnTasksInUseChanged;
        _coordinator.SettingsChanged -= OnSettingsChanged;
        if (_selectedTask != null) _coordinator.ReleaseTask(_selectedTask.Id);
        _coordinator.ReleaseColor(_windowColor);
    }

    /// <summary>Synchronously persist the draft and release this window's resources (used on close).</summary>
    public void SaveDraftAndCleanup(string? rtf)
    {
        if (_currentSession != null)
        {
            _currentSession.DraftRtf = rtf;
            _currentSession.DraftText = GetWikiText?.Invoke() ?? GetPlainText?.Invoke();
            _currentSession.OverallDurationMinutes = TotalMinutesInput;
            _currentSession.UpdatedAt = DateTime.UtcNow;
            try { _db.SaveChanges(); } catch { /* best-effort */ }
        }
        Cleanup();
    }

    public async Task LoadAsync(int? preselectIssueId = null)
    {
        var jiraSettings = await _settings.GetJiraSettingsAsync();
        JiraLinked = !string.IsNullOrWhiteSpace(jiraSettings.BaseUrl);

        var autoCfg = await _settings.GetAutoLoggingSettingsAsync();
        GlobalAutoLog = autoCfg.EnableAutoLogging;
        _coordinator.AutoLogEnabled = GlobalAutoLog;

        await LoadTasksAsync();

        // Only preselect when opened for a specific task (e.g. from the Jira Tasks list).
        // Otherwise leave nothing selected so the search picker is shown.
        _suppressSwitch = true;
        if (preselectIssueId.HasValue)
        {
            var pick = TodaysTasks.FirstOrDefault(t => t.Id == preselectIssueId.Value && !_coordinator.IsTaskInUse(t.Id));
            if (pick != null && _coordinator.TryClaimTask(pick.Id))
                _selectedTask = pick;
        }
        OnPropertyChanged(nameof(SelectedTask));
        RebuildAvailableTasks();
        _suppressSwitch = false;

        await OnSessionChangedAsync();
    }

    private async Task LoadTasksAsync()
    {
        var tasks = (await _db.JiraIssues.Where(i => i.IsActive).OrderBy(i => i.Key).ToListAsync())
            .Where(i => JiraIssue.IsAllowedType(i.IssueType));
        TodaysTasks.Clear();
        foreach (var t in tasks) TodaysTasks.Add(t);
        RebuildAvailableTasks();
        OnPropertyChanged(nameof(ShowSyncPrompt));
        OnPropertyChanged(nameof(HasTasks));
    }

    private async Task RefreshTasksAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = "Syncing Jira tasks...";
            await _jiraService.SyncIssuesAsync();
            var jiraSettings = await _settings.GetJiraSettingsAsync();
            JiraLinked = !string.IsNullOrWhiteSpace(jiraSettings.BaseUrl);
            await LoadTasksAsync();
            StatusMessage = $"{TodaysTasks.Count} tasks loaded — pick one";
        }, "Syncing...");
    }

    /// <summary>Save the current panel into the active session, then load the target session's draft.</summary>
    private async Task OnSessionChangedAsync()
    {
        if (_suppressSwitch) return;

        // 1) Persist the panel currently shown into the old session.
        if (_currentSession != null && GetRtf != null)
        {
            _currentSession.DraftRtf = GetRtf();
            _currentSession.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // 2) Resolve the target session.
        StickyNoteSession? target;
        if (_smartMode)
        {
            target = await _db.StickyNoteSessions
                .FirstOrDefaultAsync(s => s.SessionType == SessionType.SmartDistribution && s.IsOpen);
            if (target == null)
            {
                target = new StickyNoteSession { SessionType = SessionType.SmartDistribution, IsOpen = true };
                _db.StickyNoteSessions.Add(target);
                await _db.SaveChangesAsync();
            }
        }
        else if (_selectedTask != null)
        {
            target = await _db.StickyNoteSessions
                .FirstOrDefaultAsync(s => s.JiraIssueId == _selectedTask.Id && s.SessionType == SessionType.TaskSpecific && s.IsOpen);
            if (target == null)
            {
                target = new StickyNoteSession
                {
                    JiraIssueId = _selectedTask.Id,
                    SessionType = SessionType.TaskSpecific,
                    IsOpen = true
                };
                _db.StickyNoteSessions.Add(target);
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            target = null;
        }

        _currentSession = target;

        // 3) Load this session's saved time.
        // Load this task's saved time (kept only while the work is unlogged).
        _suppressMetaSave = true;
        if (target != null)
        {
            _hours = (target.OverallDurationMinutes ?? 0) / 60;
            _minutes = (target.OverallDurationMinutes ?? 0) % 60;
        }
        else { _hours = 0; _minutes = 0; }
        OnPropertyChanged(nameof(Hours));
        OnPropertyChanged(nameof(Minutes));
        _suppressMetaSave = false;

        // 4) Restore the task's unlogged draft. Submitting clears the draft, so logged work never
        //    resurfaces; only genuinely-unlogged notes are loaded back. Smart mode always starts fresh.
        if (_smartMode)
        {
            SetRtf?.Invoke(null);
            if (target != null) { target.DraftRtf = null; target.DraftText = null; await _db.SaveChangesAsync(); }
        }
        else
        {
            SetRtf?.Invoke(target?.DraftRtf);
        }
        OnPropertyChanged(nameof(CurrentBorderColor));
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool _suppressMetaSave;

    /// <summary>Persist the entered time onto the current session (used by global auto-logging).</summary>
    private void PersistSessionMeta()
    {
        if (_suppressMetaSave || _currentSession == null) return;
        _currentSession.OverallDurationMinutes = TotalMinutesInput;
        _currentSession.UpdatedAt = DateTime.UtcNow;
        try { _db.SaveChanges(); } catch { /* best effort */ }
    }

    /// <summary>Called by the window on close / autosave to persist the panel (RTF + plain mirror).</summary>
    public async Task SaveDraftAsync(string? rtf, string? plainText)
    {
        if (_currentSession == null) return;
        _currentSession.DraftRtf = rtf;
        _currentSession.DraftText = plainText;
        _currentSession.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private void SetError(string msg) { StatusIsError = true; StatusMessage = msg; }
    private void SetInfo(string msg) { StatusIsError = false; StatusMessage = msg; }

    /// <summary>The DateTime a work log is stamped with: chosen date + the current clock time.</summary>
    private DateTime StartedTimestamp => _selectedDate.Date + DateTime.Now.TimeOfDay;

    private async Task LogNowAsync()
    {
        var plain = GetPlainText?.Invoke() ?? string.Empty;
        var lines = plain.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                         .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        if (!lines.Any()) { SetError("Write something first."); return; }
        if (!JiraLinked) { SetError("Link Jira in Settings, then Sync."); return; }

        var ai = await _settings.GetAISettingsAsync();
        bool aiConfigured = !string.IsNullOrWhiteSpace(ai.BaseUrl);

        if (_smartMode)
        {
            // Smart Distribution always needs the AI to match notes to tasks.
            if (!aiConfigured) { SetError("AI model not configured. Please configure it in Settings."); return; }
            await SmartLogAsync(lines, TotalMinutesInput);
            return;
        }

        if (ai.EnablePolishing)
        {
            if (!aiConfigured) { SetError("AI model not configured. Please configure it in Settings."); return; }
            await ManualLogAsync(lines, TotalMinutesInput);
        }
        else
        {
            // Polishing off: log the written text as-is (as wiki markup), no AI, no review popup.
            await ManualLogRawAsync();
        }
    }

    /// <summary>Manual logging with AI polishing off: submit the text as written, no review popup.</summary>
    private async Task ManualLogRawAsync()
    {
        if (_selectedTask == null) { SetError("Select a task."); return; }
        if (TotalMinutesInput == 0) { SetError("Enter the time spent (hours / minutes)."); return; }
        var text = GetWikiText?.Invoke() ?? GetPlainText?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) { SetError("Write something first."); return; }

        var key = _selectedTask.Key;
        bool ok = false;
        await RunBusyAsync(async () =>
        {
            ok = await _jiraService.SubmitWorkLogAsync(key, text, TotalMinutesInput, StartedTimestamp);
            if (!ok) return;

            _db.WorkLogs.Add(new WorkLog
            {
                JiraIssueId = _selectedTask.Id,
                GeneratedText = text,
                TimeSpentMinutes = TotalMinutesInput,
                WorkDate = _selectedDate.Date,
                Status = WorkLogStatus.Submitted,
                SubmittedAt = DateTime.UtcNow
            });
            if (_currentSession != null) { _currentSession.DraftRtf = null; _currentSession.DraftText = null; }
            await _db.SaveChangesAsync();
            SetRtf?.Invoke(null);
            Hours = 0; Minutes = 0;
        }, "Logging to Jira...");

        // Status set AFTER the busy block (RunBusyAsync clears StatusMessage in its finally).
        if (ok) { _notification.ShowSuccess("Logged", $"Work log submitted to {key}."); SetInfo($"✓ Logged to {key}. Panel cleared."); }
        else SetError("Submit failed. Please check your Jira settings.");
    }

    public Func<string>? GetPlainText { get; set; }

    /// <summary>Returns the editor content as Jira wiki markup (used for auto-log / as-is submission).</summary>
    public Func<string>? GetWikiText { get; set; }

    /// <summary>Debounced autosave from the open window so the auto-logger always has current content.</summary>
    public void SaveDraftLive(string? rtf, string? wikiText)
    {
        if (_currentSession == null) return;
        _currentSession.DraftRtf = rtf;
        _currentSession.DraftText = wikiText;
        _currentSession.OverallDurationMinutes = TotalMinutesInput;
        _currentSession.UpdatedAt = DateTime.UtcNow;
        try { _db.SaveChanges(); } catch { /* best effort */ }
    }

    /// <summary>
    /// Manual mode (auto-log off): AI polishes the notes into a work log, then raises a review
    /// dialog for the single task. Submission happens after the user confirms.
    /// </summary>
    private async Task ManualLogAsync(List<string> lines, int totalMinutes)
    {
        if (_selectedTask == null) { SetError("Select a task."); return; }
        if (totalMinutes == 0) { SetError("Enter the time spent (hours / minutes)."); return; }

        string? error = null;
        ReviewRequest? review = null;
        await RunBusyAsync(async () =>
        {
            try
            {
                // Point-by-point polish (one bullet per note), same as Smart Distribution.
                var generated = await _aiService.GenerateBulletedWorkLogAsync(lines, _selectedTask.Key, _selectedTask.Summary);
                if (string.IsNullOrWhiteSpace(generated))
                { error = "The AI returned no work log. Please check your AI settings."; return; }

                var result = new SmartDistributionResult
                {
                    Issue = _selectedTask,
                    AssignedNotes = lines.Select(l => new WorkNote { Content = l }).ToList(),
                    Confidence = 1.0,
                    EstimatedMinutes = totalMinutes,
                    GeneratedWorkLog = generated
                };
                var all = (await _db.JiraIssues.Where(i => i.IsActive).ToListAsync())
                    .Where(i => JiraIssue.IsAllowedType(i.IssueType)).OrderBy(i => i.Key).ToList();
                review = new ReviewRequest(
                    new List<SmartDistributionResult> { result }, totalMinutes,
                    $"Review Work Log — {_selectedTask.Key}",
                    "AI polished your notes. Edit if needed, then submit.", all);
            }
            catch (AIRequestException ex) { error = ex.Message; }
        }, "Polishing with AI…");

        if (error != null) { SetError(error); return; }
        if (review != null) { SetInfo("Review the polished work log…"); ReviewRequested?.Invoke(this, review); }
    }

    /// <summary>
    /// Smart mode: AI matches notes to tasks and proposes a time split of the entered total.
    /// Rather than submitting, it raises <see cref="ReviewRequested"/> so the window can show
    /// an editable review dialog. Actual submission happens in <see cref="SubmitSmartAsync"/>.
    /// </summary>
    private async Task SmartLogAsync(List<string> lines, int totalMinutes)
    {
        if (totalMinutes == 0) { SetError("Enter the total time to split (hours / minutes)."); return; }

        string? error = null;
        ReviewRequest? review = null;
        await RunBusyAsync(async () =>
        {
            var issues = (await _db.JiraIssues.Where(i => i.IsActive).ToListAsync())
                .Where(i => JiraIssue.IsAllowedType(i.IssueType)).OrderBy(i => i.Key).ToList();
            if (!issues.Any()) { error = "No tasks to match. Sync Jira first."; return; }

            try
            {
                var notes = lines.Select(l => new WorkNote { Content = l, IsSmartDistribution = true }).ToList();
                var results = await _aiService.DistributeNotesAsync(notes, issues, totalMinutes);
                if (!results.Any()) { error = "The AI could not match your notes to any task. Please check your AI settings."; return; }

                NormalizeMinutes(results, totalMinutes);
                review = new ReviewRequest(
                    results, totalMinutes,
                    "Review Smart Distribution",
                    $"AI matched {results.Count} task(s) and polished each log. Adjust time & wording, then submit.", issues);
            }
            catch (AIRequestException ex) { error = ex.Message; }
        }, "AI matching & estimating…");

        if (error != null) { SetError(error); return; }
        if (review != null) { SetInfo("Review the proposed split…"); ReviewRequested?.Invoke(this, review); }
    }

    /// <summary>Scales each result's EstimatedMinutes so they add up to the user's total.</summary>
    private static void NormalizeMinutes(List<SmartDistributionResult> results, int total)
    {
        var sum = results.Sum(r => r.EstimatedMinutes);
        if (sum <= 0)
        {
            // AI gave no estimates: split evenly.
            var each = total / results.Count;
            foreach (var r in results) r.EstimatedMinutes = each;
            results[0].EstimatedMinutes += total - each * results.Count;
            return;
        }
        int running = 0;
        for (int i = 0; i < results.Count; i++)
        {
            if (i == results.Count - 1) results[i].EstimatedMinutes = total - running;
            else { results[i].EstimatedMinutes = (int)Math.Round(results[i].EstimatedMinutes * (double)total / sum); running += results[i].EstimatedMinutes; }
        }
    }

    /// <summary>Submit the reviewed/edited Smart distribution rows to Jira.</summary>
    public async Task<int> SubmitSmartAsync(IEnumerable<(int IssueId, string Key, string WorkLog, int Minutes)> rows)
    {
        int submitted = 0;
        var started = StartedTimestamp;
        await RunBusyAsync(async () =>
        {
            foreach (var row in rows)
            {
                if (row.Minutes <= 0 || string.IsNullOrWhiteSpace(row.WorkLog)) continue;
                var ok = await _jiraService.SubmitWorkLogAsync(row.Key, row.WorkLog, row.Minutes, started);
                if (!ok) continue;
                _db.WorkLogs.Add(new WorkLog
                {
                    JiraIssueId = row.IssueId,
                    GeneratedText = row.WorkLog,
                    TimeSpentMinutes = row.Minutes,
                    WorkDate = _selectedDate.Date,
                    Status = WorkLogStatus.Submitted,
                    SubmittedAt = DateTime.UtcNow
                });
                submitted++;
            }
            if (submitted > 0)
            {
                if (_currentSession != null) { _currentSession.DraftRtf = null; _currentSession.DraftText = null; }
                await _db.SaveChangesAsync();
                SetRtf?.Invoke(null);
                Hours = 0; Minutes = 0;
            }
        }, "Submitting work logs…");

        if (submitted > 0)
        {
            _notification.ShowSuccess("Work Log", $"Submitted work logs for {submitted} task(s).");
            StatusMessage = $"✓ Submitted {submitted} task(s) to Jira. Panel cleared.";
        }
        else
        {
            _notification.ShowSuccess("Work Log", "Nothing was submitted. Check Jira settings / connection.");
            StatusMessage = "✗ Submission failed — nothing was logged to Jira. See log for details.";
        }
        return submitted;
    }
}

/// <summary>Payload for opening the work-log review dialog (Smart or polished Manual).</summary>
public class ReviewRequest
{
    public List<SmartDistributionResult> Results { get; }
    public int TargetMinutes { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public List<JiraIssue> AllIssues { get; }

    public ReviewRequest(List<SmartDistributionResult> results, int targetMinutes, string title, string subtitle,
        List<JiraIssue>? allIssues = null)
    {
        Results = results;
        TargetMinutes = targetMinutes;
        Title = title;
        Subtitle = subtitle;
        AllIssues = allIssues ?? new List<JiraIssue>();
    }
}
