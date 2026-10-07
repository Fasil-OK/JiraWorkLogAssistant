using JiraWorkLogAssistant.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontStyle = System.Windows.FontStyle;
using FontWeight = System.Windows.FontWeight;

namespace JiraWorkLogAssistant.Views;

public partial class NoteWindow : Window
{
    private readonly NoteViewModel _vm;
    private readonly int? _preselectIssueId;

    public int? PreselectIssueId => _preselectIssueId;

    /// <summary>If this window is currently showing the given task in manual mode, clear it (after auto-log).</summary>
    public void ClearIfShowingTask(int issueId) => Dispatcher.Invoke(() =>
    {
        if (!_vm.SmartMode && _vm.CurrentTask?.Id == issueId)
        {
            _autoSaveTimer.Stop();
            _vm.ResetAfterAutoLog();
        }
    });

    public NoteWindow(NoteViewModel vm, int? preselectIssueId = null)
    {
        InitializeComponent();
        _vm = vm;
        _preselectIssueId = preselectIssueId;
        DataContext = vm;

        // Wire the RichTextBox <-> ViewModel bridges (RichTextBox isn't directly bindable).
        _vm.GetRtf = GetRtf;
        _vm.SetRtf = SetRtf;
        _vm.GetPlainText = GetPlainText;
        _vm.GetWikiText = GetWikiMarkup;
        _vm.AppendPoints = AppendPoints;
        _vm.AppearanceChanged += (_, _) => Dispatcher.Invoke(ApplyAppearance);

        // Debounced autosave so an open window's draft stays current for the auto-logger.
        _autoSaveTimer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(1500) };
        _autoSaveTimer.Tick += (_, _) =>
        {
            _autoSaveTimer.Stop();
            _vm.SaveDraftLive(GetRtf(), GetWikiMarkup());
        };
        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(NoteViewModel.HasTasks))
                Dispatcher.Invoke(UpdateHeaderState);
        };
        _vm.ReviewRequested += (_, req) => Dispatcher.Invoke(() => OpenReview(req));
    }

    private async void OpenReview(ReviewRequest req)
    {
        var dlg = new SmartReviewWindow(req.Results, req.TargetMinutes, req.Title, req.Subtitle, req.AllIssues) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            var rows = dlg.ConfirmedItems.Select(i => (i.IssueId, i.Key, i.WorkLog, i.TotalMinutes));
            await _vm.SubmitSmartAsync(rows);
        }
    }

    private bool _isPicking;
    private readonly DispatcherTimer _autoSaveTimer;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadAsync(_preselectIssueId);
        ApplyAppearance();
    }

    // ---- RichTextBox bridges ----
    private string? GetRtf()
    {
        var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
        if (range.IsEmpty) return null;
        using var ms = new MemoryStream();
        range.Save(ms, DataFormats.Rtf);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private string GetPlainText()
    {
        var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
        return range.Text;
    }

    // ---- Convert the editor's rich text into Jira wiki markup (for Server/DC worklogs) ----
    private string GetWikiMarkup()
    {
        var sb = new StringBuilder();
        foreach (var block in Editor.Document.Blocks)
            AppendBlock(block, sb, null);
        return sb.ToString().TrimEnd();
    }

    private void AppendBlock(Block block, StringBuilder sb, string? listPrefix)
    {
        switch (block)
        {
            case Paragraph p:
                if (listPrefix != null) sb.Append(listPrefix);
                foreach (var inline in p.Inlines)
                    AppendInline(inline, sb, false, false, false, false);
                sb.Append('\n');
                break;
            case List list:
                var prefix = list.MarkerStyle == TextMarkerStyle.Decimal ? "# " : "* ";
                foreach (var li in list.ListItems)
                    foreach (var b in li.Blocks)
                        AppendBlock(b, sb, prefix);
                break;
            case Section sec:
                foreach (var b in sec.Blocks) AppendBlock(b, sb, listPrefix);
                break;
        }
    }

    private void AppendInline(Inline inline, StringBuilder sb, bool bold, bool italic, bool underline, bool strike)
    {
        switch (inline)
        {
            case Run run:
                bool b = bold || run.FontWeight.ToOpenTypeWeight() >= 700;
                bool i = italic || run.FontStyle == FontStyles.Italic;
                bool u = underline || HasDeco(run.TextDecorations, TextDecorationLocation.Underline);
                bool s = strike || HasDeco(run.TextDecorations, TextDecorationLocation.Strikethrough);
                if (!string.IsNullOrEmpty(run.Text)) sb.Append(Wrap(run.Text, b, i, u, s));
                break;
            case LineBreak:
                sb.Append('\n');
                break;
            case Span span:
                bool sb2 = bold || span is Bold || span.FontWeight.ToOpenTypeWeight() >= 700;
                bool si = italic || span is Italic || span.FontStyle == FontStyles.Italic;
                bool su = underline || span is Underline || HasDeco(span.TextDecorations, TextDecorationLocation.Underline);
                bool ss = strike || HasDeco(span.TextDecorations, TextDecorationLocation.Strikethrough);
                foreach (var child in span.Inlines)
                    AppendInline(child, sb, sb2, si, su, ss);
                break;
        }
    }

    private static bool HasDeco(TextDecorationCollection? decos, TextDecorationLocation loc)
        => decos != null && decos.Any(d => d.Location == loc);

    private static string Wrap(string text, bool bold, bool italic, bool underline, bool strike)
    {
        // Avoid wrapping pure whitespace (Jira markup around spaces renders oddly).
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (bold) text = $"*{text}*";
        if (italic) text = $"_{text}_";
        if (underline) text = $"+{text}+";
        if (strike) text = $"-{text}-";
        return text;
    }

    /// <summary>Append fetched commit points to the editor as bullet list items.</summary>
    private void AppendPoints(IEnumerable<string> points) => Dispatcher.Invoke(() =>
    {
        var list = new List { MarkerStyle = TextMarkerStyle.Disc };
        foreach (var p in points)
            list.ListItems.Add(new ListItem(new Paragraph(new Run(p))));
        if (list.ListItems.Count == 0) return;

        Editor.Document.Blocks.Add(list);
        Editor.CaretPosition = Editor.Document.ContentEnd;
        Editor.Focus();
    });

    private void SetRtf(string? rtf)
    {
        Editor.Document.Blocks.Clear();
        if (string.IsNullOrWhiteSpace(rtf)) return;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(rtf);
            using var ms = new MemoryStream(bytes);
            var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
            range.Load(ms, DataFormats.Rtf);
        }
        catch { /* corrupt draft -> start empty */ }
    }

    // Light paper the sticky color is tinted into (so notes look like real sticky notes).
    private static readonly Color Paper = Color.FromRgb(0xFF, 0xFF, 0xFF);

    private static Color Mix(Color accent, double accentRatio)
    {
        double r = accentRatio;
        return Color.FromRgb(
            (byte)(accent.R * r + Paper.R * (1 - r)),
            (byte)(accent.G * r + Paper.G * (1 - r)),
            (byte)(accent.B * r + Paper.B * (1 - r)));
    }

    private void ApplyAppearance()
    {
        try
        {
            // The whole window is tinted in light shades of the window's sticky color.
            var accent = (Color)System.Windows.Media.ColorConverter.ConvertFromString(_vm.CurrentBorderColor);

            RootBorder.Background = new SolidColorBrush(Mix(accent, 0.14)); // body
            HeaderBar.Background = new SolidColorBrush(Mix(accent, 0.30));  // header
            ToolbarBar.Background = new SolidColorBrush(Mix(accent, 0.20)); // toolbar
            FooterBar.Background = new SolidColorBrush(Mix(accent, 0.26));  // footer
            Editor.Background = new SolidColorBrush(Mix(accent, 0.05));     // writing paper (near white)

            if (_vm.SmartMode)
            {
                RootBorder.BorderBrush = new LinearGradientBrush(
                    Color.FromRgb(0x7C, 0x3A, 0xED), Color.FromRgb(0xEC, 0x48, 0x99), 45);
            }
            else
            {
                RootBorder.BorderBrush = new SolidColorBrush(accent);
            }

            UpdateHeaderState();
        }
        catch { }
    }

    /// <summary>
    /// Drives the header (smart label / selected-task display / hint) and the search-picker row
    /// that sits between the header and the formatting toolbar.
    /// </summary>
    private void UpdateHeaderState()
    {
        if (_vm.SmartMode)
        {
            SmartLabel.Visibility = Visibility.Visible;
            SelectedPanel.Visibility = Visibility.Collapsed;
            HeaderHint.Visibility = Visibility.Collapsed;
            PickerRow.Visibility = Visibility.Collapsed;
            return;
        }

        SmartLabel.Visibility = Visibility.Collapsed;

        // Before any sync there are no tasks — the sync banner guides the user instead.
        if (!_vm.HasTasks)
        {
            SelectedPanel.Visibility = Visibility.Collapsed;
            HeaderHint.Visibility = Visibility.Collapsed;
            PickerRow.Visibility = Visibility.Collapsed;
            return;
        }

        if (_vm.SelectedTask != null && !_isPicking)
        {
            // Task chosen: show "KEY — summary" + pencil in the header; hide the picker row.
            SelectedTaskText.Text = $"{_vm.SelectedTask.Key} — {_vm.SelectedTask.Summary}";
            SelectedPanel.Visibility = Visibility.Visible;
            HeaderHint.Visibility = Visibility.Collapsed;
            PickerRow.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Choosing a task: hint in the header, searchable dropdown in its own row below.
            SelectedPanel.Visibility = Visibility.Collapsed;
            HeaderHint.Visibility = Visibility.Visible;
            PickerRow.Visibility = Visibility.Visible;
        }
    }

    // ---- Searchable task picker ----
    private void EditTask_Click(object sender, RoutedEventArgs e)
    {
        _isPicking = true;
        _vm.TaskSearch = string.Empty;
        UpdateHeaderState();
        TaskCombo.Focus();
        TaskCombo.IsDropDownOpen = true;
    }

    private void TaskCombo_GotFocus(object sender, RoutedEventArgs e)
    {
        if (PickerRow.Visibility == Visibility.Visible)
            TaskCombo.IsDropDownOpen = true;
    }

    private void TaskCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not JiraWorkLogAssistant.Core.Models.JiraIssue issue) return;

        _isPicking = false;
        TaskCombo.IsDropDownOpen = false;
        _vm.SmartMode = false;
        _vm.SelectedTask = issue;   // claims via coordinator; triggers ApplyAppearance -> UpdateHeaderState
        _vm.TaskSearch = string.Empty;
    }

    // ---- Formatting: B / I / U / strikethrough ----
    private void Bold_Click(object sender, RoutedEventArgs e)
    { EditingCommands.ToggleBold.Execute(null, Editor); Editor.Focus(); }

    private void Italic_Click(object sender, RoutedEventArgs e)
    { EditingCommands.ToggleItalic.Execute(null, Editor); Editor.Focus(); }

    private void Underline_Click(object sender, RoutedEventArgs e)
    { EditingCommands.ToggleUnderline.Execute(null, Editor); Editor.Focus(); }

    private void Strikethrough_Click(object sender, RoutedEventArgs e)
    {
        var sel = Editor.Selection;
        var current = sel.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        bool has = current != null && current.Any(d => d.Location == TextDecorationLocation.Strikethrough);
        sel.ApplyPropertyValue(Inline.TextDecorationsProperty,
            has ? null : TextDecorations.Strikethrough);
        Editor.Focus();
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Restart the debounce so we save ~1.5s after the user stops typing.
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    // Reflect the formatting of the current selection in the toggle buttons.
    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var sel = Editor.Selection;
        BoldBtn.IsChecked = sel.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight fw && fw == FontWeights.Bold;
        ItalicBtn.IsChecked = sel.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle fs && fs == FontStyles.Italic;
        var dec = sel.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        UnderlineBtn.IsChecked = dec != null && dec.Any(d => d.Location == TextDecorationLocation.Underline);
    }

    // ---- Formatting: font size ----
    private void FontUp_Click(object sender, RoutedEventArgs e) => BumpFontSize(1);
    private void FontDown_Click(object sender, RoutedEventArgs e) => BumpFontSize(-1);

    private void BumpFontSize(double delta)
    {
        var sel = Editor.Selection;
        var current = sel.GetPropertyValue(TextElement.FontSizeProperty);
        double size = current is double d ? d : Editor.FontSize;
        size = Math.Max(8, Math.Min(40, size + delta));
        sel.ApplyPropertyValue(TextElement.FontSizeProperty, size);
    }

    // ---- Window chrome ----
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void DateButton_Click(object sender, RoutedEventArgs e) => DatePopup.IsOpen = true;

    private void DateCalendar_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DateCalendar.SelectedDate.HasValue) DatePopup.IsOpen = false;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Warn if there is unlogged work in the panel.
        if (!string.IsNullOrWhiteSpace(GetPlainText()))
        {
            var result = MessageBox.Show(this,
                "This note has work that hasn't been logged to Jira yet.\n\nClose it anyway? The unlogged notes will not be submitted.",
                "Unlogged work",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes) { e.Cancel = true; return; }
        }

        // Persist the draft (so it can be restored) and free this window's task + color.
        _vm.SaveDraftAndCleanup(GetRtf());
    }
}
