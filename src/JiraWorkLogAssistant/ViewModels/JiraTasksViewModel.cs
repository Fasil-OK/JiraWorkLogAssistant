using CommunityToolkit.Mvvm.Input;
using JiraWorkLogAssistant.Core.Data;
using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using JiraWorkLogAssistant.ViewModels.Base;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace JiraWorkLogAssistant.ViewModels;

public class JiraTasksViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private readonly IJiraService _jiraService;

    public ObservableCollection<JiraIssue> Issues { get; } = new();
    public ObservableCollection<JiraIssue> FilteredIssues { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { SetProperty(ref _searchText, value); ApplyFilter(); }
    }

    private JiraIssue? _selectedIssue;
    public JiraIssue? SelectedIssue
    {
        get => _selectedIssue;
        set => SetProperty(ref _selectedIssue, value);
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand<JiraIssue> OpenNoteWindowCommand { get; }

    public event EventHandler<JiraIssue>? OpenNoteWindowRequested;

    public JiraTasksViewModel(AppDbContext db, IJiraService jiraService)
    {
        _db = db;
        _jiraService = jiraService;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenNoteWindowCommand = new RelayCommand<JiraIssue>(issue =>
        {
            if (issue != null) OpenNoteWindowRequested?.Invoke(this, issue);
        });
    }

    public async Task LoadAsync()
    {
        var issues = (await _db.JiraIssues
            .Where(i => i.IsActive)
            .OrderBy(i => i.Key)
            .ToListAsync())
            .Where(i => JiraIssue.IsAllowedType(i.IssueType)); // Task, Sub-task, Bug, Improvement only
        Issues.Clear();
        foreach (var i in issues) Issues.Add(i);
        ApplyFilter();
    }

    private async Task RefreshAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = "Syncing with Jira...";
            await _jiraService.SyncIssuesAsync();
            await LoadAsync();
            StatusMessage = $"Synced {Issues.Count} issues";
        }, "Syncing...");
    }

    private void ApplyFilter()
    {
        FilteredIssues.Clear();
        var search = _searchText.ToLower();
        foreach (var i in Issues)
        {
            if (string.IsNullOrWhiteSpace(search) ||
                i.Key.ToLower().Contains(search) ||
                i.Summary.ToLower().Contains(search))
            {
                FilteredIssues.Add(i);
            }
        }
    }
}
