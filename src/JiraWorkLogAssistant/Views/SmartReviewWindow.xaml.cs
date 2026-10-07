using JiraWorkLogAssistant.Core.Models;
using JiraWorkLogAssistant.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace JiraWorkLogAssistant.Views;

public partial class SmartReviewWindow : Window
{
    private readonly ObservableCollection<SmartReviewItem> _items = new();
    private readonly int _targetMinutes;

    /// <summary>Populated when the user confirms; consumed by the caller to submit.</summary>
    public List<SmartReviewItem> ConfirmedItems { get; } = new();

    public SmartReviewWindow(List<SmartDistributionResult> results, int targetMinutes,
        string title = "Review", string subtitle = "", List<JiraIssue>? allIssues = null)
    {
        InitializeComponent();
        _targetMinutes = targetMinutes;
        TitleText.Text = title;

        foreach (var r in results)
            _items.Add(NewItem(r.Issue.Id, r.Issue.Key, r.Issue.Summary,
                string.Join("  •  ", r.AssignedNotes.Select(n => n.Content)),
                (int)Math.Round(r.Confidence * 100), r.GeneratedWorkLog, r.EstimatedMinutes));

        ItemsList.ItemsSource = _items;
        SubTitle.Text = subtitle;

        // Populate the "add task" dropdown with any allowed tasks not already listed.
        if (allIssues != null)
            AddTaskCombo.ItemsSource = allIssues;

        UpdateTotal();
    }

    private SmartReviewItem NewItem(int id, string key, string summary, string notes, int conf, string log, int minutes)
    {
        var item = new SmartReviewItem
        {
            IssueId = id, Key = key, Summary = summary, Notes = notes,
            ConfidencePercent = conf, WorkLog = log,
            Hours = minutes / 60, Minutes = minutes % 60
        };
        item.PropertyChanged += Item_PropertyChanged;
        return item;
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        if (AddTaskCombo.SelectedItem is not JiraIssue issue) return;
        if (_items.Any(i => i.IssueId == issue.Id))
        {
            MessageBox.Show(this, $"{issue.Key} is already in the list.", "Add task",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _items.Add(NewItem(issue.Id, issue.Key, issue.Summary, "(manually added)", 100, string.Empty, 0));
        AddTaskCombo.SelectedItem = null;
        AddTaskCombo.Text = string.Empty;
        UpdateTotal();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SmartReviewItem.Hours) or nameof(SmartReviewItem.Minutes))
            UpdateTotal();
    }

    private void UpdateTotal()
    {
        var total = _items.Sum(i => i.TotalMinutes);
        var h = total / 60; var m = total % 60;
        var th = _targetMinutes / 60; var tm = _targetMinutes % 60;
        TotalText.Text = $"{h}h {m}m  /  target {th}h {tm}m";
        TotalText.Foreground = total == _targetMinutes
            ? System.Windows.Media.Brushes.LightGreen
            : System.Windows.Media.Brushes.Orange;
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SmartReviewItem item)
        {
            item.PropertyChanged -= Item_PropertyChanged;
            _items.Remove(item);
            UpdateTotal();
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        ConfirmedItems.Clear();
        ConfirmedItems.AddRange(_items.Where(i => i.TotalMinutes > 0 && !string.IsNullOrWhiteSpace(i.WorkLog)));
        if (ConfirmedItems.Count == 0)
        {
            MessageBox.Show(this, "Nothing to submit — set time and a work log on at least one task.",
                "Review", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
