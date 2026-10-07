using JiraWorkLogAssistant.Core.Data;
using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using JiraWorkLogAssistant.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace JiraWorkLogAssistant.Views;

public class WindowManager
{
    private readonly IServiceProvider _services;
    private DashboardWindow? _dashboard;
    private JiraTasksWindow? _jiraTasks;
    private SettingsWindow? _settings;

    // Unified capture windows (sticky-note style with task dropdown + smart toggle). Multiple allowed.
    private readonly List<(NoteWindow Window, IServiceScope Scope)> _notes = new();

    public WindowManager(IServiceProvider services)
    {
        _services = services;
    }

    private static void Run(Action action) => Application.Current.Dispatcher.Invoke(action);

    public void ShowDashboard() => Run(() =>
    {
        if (_dashboard == null || !_dashboard.IsLoaded)
        {
            var vm = _services.GetRequiredService<DashboardViewModel>();
            _dashboard = new DashboardWindow(vm);
            _dashboard.Closed += (_, _) => _dashboard = null;
        }
        _dashboard.Show(); _dashboard.Activate();
    });

    public void ShowJiraTasks() => Run(() =>
    {
        if (_jiraTasks == null || !_jiraTasks.IsLoaded)
        {
            var vm = _services.GetRequiredService<JiraTasksViewModel>();
            vm.OpenNoteWindowRequested += (_, issue) => ShowNoteWindow(issue.Id);
            _jiraTasks = new JiraTasksWindow(vm);
            _jiraTasks.Closed += (_, _) => _jiraTasks = null;
        }
        _jiraTasks.Show(); _jiraTasks.Activate();
    });

    public void ShowSettings() => Run(() =>
    {
        if (_settings == null || !_settings.IsLoaded)
        {
            var vm = _services.GetRequiredService<SettingsViewModel>();
            _settings = new SettingsWindow(vm);
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show(); _settings.Activate();
    });

    /// <summary>Opens a new capture window. Multiple may be open at once, each with its own task dropdown.</summary>
    public void ShowNoteWindow(int? preselectIssueId = null) => Run(() =>
    {
        // If a window is already showing this exact task, just focus it.
        if (preselectIssueId.HasValue)
        {
            var match = _notes.FirstOrDefault(n => n.Window.PreselectIssueId == preselectIssueId);
            if (match.Window != null) { match.Window.Activate(); return; }
        }

        var scope = _services.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<NoteViewModel>();
        var window = new NoteWindow(vm, preselectIssueId);
        var entry = (window, scope);
        _notes.Add(entry);
        window.Closed += (_, _) =>
        {
            scope.Dispose();
            _notes.RemoveAll(n => ReferenceEquals(n.Window, window));
        };

        // Cascade new windows so they don't stack exactly on top of each other.
        var offset = _notes.Count * 28;
        window.Left = 120 + offset;
        window.Top = 120 + offset;
        window.Show(); window.Activate();
    });

    // Kept for menu/back-compat: opens the unified capture window.
    public void ShowNewNote() => ShowNoteWindow();
    public void OpenStickyNote(JiraIssue issue) => ShowNoteWindow(issue.Id);

    /// <summary>Snap all open note windows to the right edge of the screen, stacked, always-on-top.</summary>
    public void PinNotesRight() => Run(() =>
    {
        var area = SystemParameters.WorkArea;
        const double gap = 8;
        double top = area.Top + gap;
        double columnRight = area.Right - gap;

        foreach (var entry in _notes)
        {
            var w = entry.Window;
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;

            // Move to a new column if this window would overflow the bottom.
            if (top + w.Height > area.Bottom && top > area.Top + gap)
            {
                top = area.Top + gap;
                columnRight -= w.Width + gap;
            }

            w.Left = Math.Max(area.Left, columnRight - w.Width);
            w.Top = top;
            w.Topmost = true;
            w.Activate();

            top += w.Height + gap;
        }
    });

    /// <summary>Called by the auto-logger after it submits a task's notes — clear any window showing it.</summary>
    public void CloseStickyNote(int issueId) => Run(() =>
    {
        foreach (var entry in _notes.ToList())
            entry.Window.ClearIfShowingTask(issueId);
    });

    public async Task SyncJiraAsync()
    {
        using var scope = _services.CreateScope();
        var jira = scope.ServiceProvider.GetRequiredService<IJiraService>();
        var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await jira.SyncIssuesAsync();
        notif.ShowSuccess("Sync Complete", "Jira issues synchronized.");
    }

    public async Task RestoreSessionsAsync()
    {
        bool anyOpen;
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            anyOpen = await db.StickyNoteSessions.AnyAsync(s => s.IsOpen);
        }
        if (anyOpen) ShowNoteWindow();
    }
}
