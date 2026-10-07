using JiraWorkLogAssistant.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace JiraWorkLogAssistant.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadAsync();
        JiraTokenBox.Password = _vm.JiraPassword;
        AIKeyBox.Password = _vm.AIApiKey;
        BbPasswordBox.Password = _vm.BbPassword;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void JiraToken_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.JiraPassword = JiraTokenBox.Password;

    private void AIKey_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.AIApiKey = AIKeyBox.Password;

    private void BbPassword_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.BbPassword = BbPasswordBox.Password;
}
