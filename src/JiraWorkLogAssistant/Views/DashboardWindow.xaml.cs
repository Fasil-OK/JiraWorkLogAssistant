using JiraWorkLogAssistant.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace JiraWorkLogAssistant.Views;

public partial class DashboardWindow : Window
{
    private readonly DashboardViewModel _vm;

    public DashboardWindow(DashboardViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
        => await _vm.LoadAsync();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
