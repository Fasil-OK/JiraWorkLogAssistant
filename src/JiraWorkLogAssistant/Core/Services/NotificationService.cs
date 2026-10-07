using JiraWorkLogAssistant.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace JiraWorkLogAssistant.Core.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public void SetNotifyIcon(System.Windows.Forms.NotifyIcon icon) => _notifyIcon = icon;

    public void ShowInfo(string title, string message) => Show(title, message, System.Windows.Forms.ToolTipIcon.Info);
    public void ShowSuccess(string title, string message) => Show(title, message, System.Windows.Forms.ToolTipIcon.Info);
    public void ShowWarning(string title, string message) => Show(title, message, System.Windows.Forms.ToolTipIcon.Warning);
    public void ShowError(string title, string message) => Show(title, message, System.Windows.Forms.ToolTipIcon.Error);

    private void Show(string title, string message, System.Windows.Forms.ToolTipIcon icon)
    {
        _logger.LogInformation("[{Icon}] {Title}: {Message}", icon, title, message);
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            _notifyIcon?.ShowBalloonTip(4000, title, message, icon);
        });
    }
}
