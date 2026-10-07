using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JiraWorkLogAssistant.Views.Controls;

/// <summary>Compact numeric up/down (also accepts keyboard input). Used for Hours/Minutes entry.</summary>
public partial class NumericSpinner : UserControl
{
    private bool _updating;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(NumericSpinner),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(NumericSpinner), new PropertyMetadata(0));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(NumericSpinner), new PropertyMetadata(59));

    public int Value { get => (int)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public NumericSpinner()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncBox();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((NumericSpinner)d).SyncBox();

    private void SyncBox()
    {
        if (_updating) return;
        _updating = true;
        var clamped = Clamp(Value);
        if (clamped != Value) Value = clamped;
        if (ValueBox.Text != clamped.ToString()) ValueBox.Text = clamped.ToString();
        _updating = false;
    }

    private int Clamp(int v) => v < Minimum ? Minimum : (v > Maximum ? Maximum : v);

    private void Up_Click(object sender, RoutedEventArgs e) { Value = Clamp(Value + 1); ValueBox.Focus(); }
    private void Down_Click(object sender, RoutedEventArgs e) { Value = Clamp(Value - 1); ValueBox.Focus(); }

    private void ValueBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");

    private void ValueBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        _updating = true;
        if (int.TryParse(ValueBox.Text, out var v))
            Value = Clamp(v);
        else if (string.IsNullOrEmpty(ValueBox.Text))
            Value = Minimum;
        _updating = false;
    }
}
