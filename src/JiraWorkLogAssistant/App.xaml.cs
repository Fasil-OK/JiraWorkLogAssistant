using JiraWorkLogAssistant.Core.Data;
using JiraWorkLogAssistant.Core.Services;
using JiraWorkLogAssistant.Core.Services.Interfaces;
using JiraWorkLogAssistant.ViewModels;
using JiraWorkLogAssistant.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Windows;

namespace JiraWorkLogAssistant;

public partial class App : Application
{
    private IHost? _host;
    private TrayIconService? _trayService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JiraWorkLogAssistant", "logs", "app-.log");

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await InitializeDatabaseAsync();
        await _host.StartAsync();

        _trayService = _host.Services.GetRequiredService<TrayIconService>();
        _trayService.Initialize();

        // Allow the background auto-logger to close task windows after it submits & clears notes.
        var manager = _host.Services.GetRequiredService<WindowManager>();
        Core.Services.AutoLoggingService.CloseWindowCallback = manager.CloseStickyNote;

        await RestoreOpenSessionsAsync();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite($"Data Source={AppDbContext.GetDbPath()}"));

        services.AddSingleton<EncryptionService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IJiraService, JiraService>();
        services.AddScoped<IBitbucketService, BitbucketService>();
        services.AddScoped<IAIService, AIService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<WindowManager>();
        services.AddScoped<GitIntegrationService>();
        services.AddHostedService<AutoLoggingService>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<JiraTasksViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<NoteCoordinator>();
        services.AddTransient<NoteViewModel>();
    }

    private async Task InitializeDatabaseAsync()
    {
        using var scope = _host!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    private async Task RestoreOpenSessionsAsync()
    {
        var manager = _host!.Services.GetRequiredService<WindowManager>();
        await manager.RestoreSessionsAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
