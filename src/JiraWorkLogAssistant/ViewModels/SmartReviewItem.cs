using CommunityToolkit.Mvvm.ComponentModel;

namespace JiraWorkLogAssistant.ViewModels;

/// <summary>One editable row in the Smart Distribution review dialog.</summary>
public partial class SmartReviewItem : ObservableObject
{
    public int IssueId { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public int ConfidencePercent { get; init; }
    public bool IsLowConfidence => ConfidencePercent < 70;
    public string ConfidenceText => $"{ConfidencePercent}% match";

    [ObservableProperty] private string _workLog = string.Empty;
    [ObservableProperty] private int _hours;
    [ObservableProperty] private int _minutes;

    public int TotalMinutes => Hours * 60 + Minutes;
}
