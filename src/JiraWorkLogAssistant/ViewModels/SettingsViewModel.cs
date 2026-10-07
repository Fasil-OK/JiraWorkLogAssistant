using CommunityToolkit.Mvvm.Input;
using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.Core.Services;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using JiraWorkLogAssistant.ViewModels.Base;
using Microsoft.Win32;
using System.Linq;

namespace JiraWorkLogAssistant.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IJiraService _jiraService;
    private readonly IAIService _aiService;
    private readonly IBitbucketService _bitbucketService;

    // Bitbucket
    private string _bbUsername = string.Empty;
    public string BbUsername { get => _bbUsername; set => SetProperty(ref _bbUsername, value); }

    private string _bbPassword = string.Empty;
    public string BbPassword { get => _bbPassword; set => SetProperty(ref _bbPassword, value); }

    private string _bbRepoUrls = string.Empty;
    public string BbRepoUrls { get => _bbRepoUrls; set => SetProperty(ref _bbRepoUrls, value); }

    private string _bbStatus = string.Empty;
    public string BbStatus { get => _bbStatus; set => SetProperty(ref _bbStatus, value); }

    private bool _bbStatusIsError;
    public bool BbStatusIsError { get => _bbStatusIsError; set => SetProperty(ref _bbStatusIsError, value); }

    public IAsyncRelayCommand TestBitbucketCommand { get; }
    public IAsyncRelayCommand SaveBitbucketCommand { get; }

    // Jira
    private string _jiraBaseUrl = string.Empty;
    public string JiraBaseUrl { get => _jiraBaseUrl; set => SetProperty(ref _jiraBaseUrl, value); }

    private string _jiraUsername = string.Empty;
    public string JiraUsername { get => _jiraUsername; set => SetProperty(ref _jiraUsername, value); }

    private string _jiraPassword = string.Empty;
    public string JiraPassword { get => _jiraPassword; set => SetProperty(ref _jiraPassword, value); }

    private string _jiraStatus = string.Empty;
    public string JiraStatus { get => _jiraStatus; set => SetProperty(ref _jiraStatus, value); }

    // AI
    private string _aiBaseUrl = string.Empty;
    public string AIBaseUrl { get => _aiBaseUrl; set => SetProperty(ref _aiBaseUrl, value); }

    private string _aiApiKey = string.Empty;
    public string AIApiKey { get => _aiApiKey; set => SetProperty(ref _aiApiKey, value); }

    private string _aiModelName = string.Empty;
    public string AIModelName { get => _aiModelName; set => SetProperty(ref _aiModelName, value); }

    private string _aiWorkLogPrompt = string.Empty;
    public string AIWorkLogPrompt { get => _aiWorkLogPrompt; set => SetProperty(ref _aiWorkLogPrompt, value); }

    /// <summary>Shown as the placeholder/hint so the user can see the default they'd otherwise get.</summary>
    public string DefaultWorkLogPrompt => Core.Services.AIService.DefaultWorkLogSystemPrompt;

    private string _aiStatus = string.Empty;
    public string AIStatus { get => _aiStatus; set => SetProperty(ref _aiStatus, value); }

    private bool _aiStatusIsError;
    public bool AIStatusIsError { get => _aiStatusIsError; set => SetProperty(ref _aiStatusIsError, value); }

    private bool _enablePolishing = true;
    public bool EnablePolishing { get => _enablePolishing; set => SetProperty(ref _enablePolishing, value); }

    // Auto Logging
    private bool _enableAutoLogging;
    public bool EnableAutoLogging { get => _enableAutoLogging; set => SetProperty(ref _enableAutoLogging, value); }

    private string _autoLoggingTime = "18:00";
    public string AutoLoggingTime { get => _autoLoggingTime; set => SetProperty(ref _autoLoggingTime, value); }

    private bool _requireApproval = true;
    public bool RequireApproval { get => _requireApproval; set => SetProperty(ref _requireApproval, value); }

    private bool _enableDailySummary = true;
    public bool EnableDailySummary { get => _enableDailySummary; set => SetProperty(ref _enableDailySummary, value); }

    private bool _archiveNotes = true;
    public bool ArchiveNotes { get => _archiveNotes; set => SetProperty(ref _archiveNotes, value); }

    private bool _closeWindows = true;
    public bool CloseWindows { get => _closeWindows; set => SetProperty(ref _closeWindows, value); }

    private bool _launchOnStartup = true;
    public bool LaunchOnStartup
    {
        get => _launchOnStartup;
        set { SetProperty(ref _launchOnStartup, value); ApplyStartupSetting(value); }
    }

    private bool _showIdleNotification = true;
    public bool ShowIdleNotification { get => _showIdleNotification; set => SetProperty(ref _showIdleNotification, value); }

    private int _idleMinutes = 120;
    public int IdleMinutes { get => _idleMinutes; set => SetProperty(ref _idleMinutes, value); }

    private double _confidenceThreshold = 0.70;
    public double ConfidenceThreshold { get => _confidenceThreshold; set => SetProperty(ref _confidenceThreshold, value); }

    public IAsyncRelayCommand TestJiraCommand { get; }
    public IAsyncRelayCommand SaveJiraCommand { get; }
    public IAsyncRelayCommand TestAICommand { get; }
    public IAsyncRelayCommand SaveAICommand { get; }
    public IAsyncRelayCommand SaveAutoLoggingCommand { get; }

    private readonly NoteCoordinator _coordinator;

    public SettingsViewModel(ISettingsService settings, IJiraService jira, IAIService ai,
        IBitbucketService bitbucket, NoteCoordinator coordinator)
    {
        _settings = settings;
        _jiraService = jira;
        _aiService = ai;
        _bitbucketService = bitbucket;
        _coordinator = coordinator;

        TestJiraCommand = new AsyncRelayCommand(TestJiraAsync);
        SaveJiraCommand = new AsyncRelayCommand(SaveJiraAsync);
        TestAICommand = new AsyncRelayCommand(TestAIAsync);
        SaveAICommand = new AsyncRelayCommand(SaveAIAsync);
        SaveAutoLoggingCommand = new AsyncRelayCommand(SaveAutoLoggingAsync);
        TestBitbucketCommand = new AsyncRelayCommand(TestBitbucketAsync);
        SaveBitbucketCommand = new AsyncRelayCommand(SaveBitbucketAsync);
    }

    private BitbucketSettings BuildBitbucketSettings() => new()
    {
        Username = BbUsername,
        Password = BbPassword,
        RepoUrls = (BbRepoUrls ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim()).Where(u => u.Length > 0).ToList()
    };

    private async Task TestBitbucketAsync()
    {
        await RunBusyAsync(async () =>
        {
            var ok = await _bitbucketService.TestConnectionAsync(BuildBitbucketSettings());
            BbStatusIsError = !ok;
            BbStatus = ok ? "Bitbucket connected successfully!" : "Bitbucket connection failed. Check URL / credentials.";
        }, "Testing...");
    }

    private async Task SaveBitbucketAsync()
    {
        await _settings.SaveBitbucketSettingsAsync(BuildBitbucketSettings());
        BbStatusIsError = false;
        BbStatus = "Bitbucket settings saved.";
    }

    public async Task LoadAsync()
    {
        var jira = await _settings.GetJiraSettingsAsync();
        JiraBaseUrl = jira.BaseUrl;
        JiraUsername = jira.Username;
        JiraPassword = jira.Password;

        var ai = await _settings.GetAISettingsAsync();
        // Pre-fill with the saved custom prompt, or the built-in default so the user can edit it.
        AIWorkLogPrompt = string.IsNullOrWhiteSpace(ai.WorkLogSystemPrompt)
            ? DefaultWorkLogPrompt : ai.WorkLogSystemPrompt;
        AIBaseUrl = ai.BaseUrl;

        var bb = await _settings.GetBitbucketSettingsAsync();
        BbUsername = bb.Username;
        BbPassword = bb.Password;
        BbRepoUrls = string.Join("\n", bb.RepoUrls);
        AIApiKey = ai.ApiKey;
        AIModelName = ai.ModelName;
        EnablePolishing = ai.EnablePolishing;

        var auto = await _settings.GetAutoLoggingSettingsAsync();
        EnableAutoLogging = auto.EnableAutoLogging;
        AutoLoggingTime = $"{auto.AutoLoggingTime.Hours:D2}:{auto.AutoLoggingTime.Minutes:D2}";
        RequireApproval = auto.RequireApprovalBeforeSubmission;
        EnableDailySummary = auto.EnableDailySummary;
        ArchiveNotes = auto.ArchiveNotesAfterSubmission;
        CloseWindows = auto.CloseWindowsAfterSubmission;
        LaunchOnStartup = auto.LaunchOnStartup;
        ShowIdleNotification = auto.ShowIdleNotification;
        IdleMinutes = auto.IdleNotificationMinutes;
        ConfidenceThreshold = auto.ConfidenceThreshold;
    }

    private async Task TestJiraAsync()
    {
        await RunBusyAsync(async () =>
        {
            var ok = await _jiraService.TestConnectionAsync(new JiraSettings { BaseUrl = JiraBaseUrl, Username = JiraUsername, Password = JiraPassword });
            JiraStatus = ok ? "Connected successfully!" : "Connection failed. Check credentials.";
        }, "Testing...");
    }

    private async Task SaveJiraAsync()
    {
        await _settings.SaveJiraSettingsAsync(new JiraSettings { BaseUrl = JiraBaseUrl, Username = JiraUsername, Password = JiraPassword });
        JiraStatus = "Jira settings saved.";
    }

    private async Task TestAIAsync()
    {
        await RunBusyAsync(async () =>
        {
            var ok = await _aiService.TestConnectionAsync(new AISettings { BaseUrl = AIBaseUrl, ApiKey = AIApiKey, ModelName = AIModelName });
            AIStatusIsError = !ok;
            AIStatus = ok ? "AI connected successfully!" : "AI connection failed. Check settings.";
        }, "Testing...");
    }

    private async Task SaveAIAsync()
    {
        // If the box is blank or left as the unedited default, store empty (so it always uses the live default).
        var prompt = AIWorkLogPrompt?.Trim() ?? string.Empty;
        if (prompt == DefaultWorkLogPrompt.Trim()) prompt = string.Empty;
        await _settings.SaveAISettingsAsync(new AISettings { BaseUrl = AIBaseUrl, ApiKey = AIApiKey, ModelName = AIModelName, WorkLogSystemPrompt = prompt, EnablePolishing = EnablePolishing });
        AIStatusIsError = false;
        AIStatus = "AI settings saved.";
    }

    private async Task SaveAutoLoggingAsync()
    {
        if (!TimeSpan.TryParse(AutoLoggingTime, out var ts)) ts = new TimeSpan(18, 0, 0);
        await _settings.SaveAutoLoggingSettingsAsync(new AutoLoggingSettings
        {
            EnableAutoLogging = EnableAutoLogging,
            AutoLoggingTime = ts,
            RequireApprovalBeforeSubmission = RequireApproval,
            EnableDailySummary = EnableDailySummary,
            ArchiveNotesAfterSubmission = ArchiveNotes,
            CloseWindowsAfterSubmission = CloseWindows,
            LaunchOnStartup = LaunchOnStartup,
            ShowIdleNotification = ShowIdleNotification,
            IdleNotificationMinutes = IdleMinutes,
            ConfidenceThreshold = ConfidenceThreshold
        });
        // Notify open note windows so the "Log now" button updates live.
        _coordinator.AutoLogEnabled = EnableAutoLogging;
        _coordinator.NotifySettingsChanged();
        StatusMessage = "Auto logging settings saved.";
    }

    private void ApplyStartupSetting(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (enable)
                key?.SetValue("JiraWorkLogAssistant", $"\"{exe}\"");
            else
                key?.DeleteValue("JiraWorkLogAssistant", false);
        }
        catch { }
    }
}
