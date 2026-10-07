using JiraWorkLogAssistant.Core.Services.Interfaces;
using JiraWorkLogAssistant.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Drawing;
using System.Windows.Forms;

namespace JiraWorkLogAssistant.Views;

public class TrayIconService : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly NotificationService _notification;
    private readonly WindowManager _windowManager;
    private NotifyIcon? _notifyIcon;

    public TrayIconService(IServiceProvider services, NotificationService notification, WindowManager windowManager)
    {
        _services = services;
        _notification = notification;
        _windowManager = windowManager;
    }

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Jira WorkLog Assistant",
            Visible = true,
            Icon = GetIcon()
        };
        _notification.SetNotifyIcon(_notifyIcon);

        var menu = new ContextMenuStrip();
        menu.Items.Add("New Note", null, (_, _) => _windowManager.ShowNewNote());
        menu.Items.Add("Jira Tasks", null, (_, _) => _windowManager.ShowJiraTasks());
        menu.Items.Add("Dashboard", null, (_, _) => _windowManager.ShowDashboard());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sync Jira", null, async (_, _) => await _windowManager.SyncJiraAsync());
        menu.Items.Add("Settings", null, (_, _) => _windowManager.ShowSettings());
        menu.Items.Add("Pin Notes Right", null, (_, _) => _windowManager.PinNotesRight());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => _windowManager.ShowNewNote();
    }

    private static Icon GetIcon()
    {
        // Create a simple icon programmatically
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(124, 58, 237));
            g.FillEllipse(Brushes.White, 3, 3, 10, 10);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void ExitApp()
    {
        _notifyIcon?.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
