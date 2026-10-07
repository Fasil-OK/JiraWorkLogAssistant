using System.IO;
using JiraWorkLogAssistant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace JiraWorkLogAssistant.Core.Data;

public class AppDbContext : DbContext
{
    public DbSet<JiraIssue> JiraIssues => Set<JiraIssue>();
    public DbSet<WorkNote> WorkNotes => Set<WorkNote>();
    public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<StickyNoteSession> StickyNoteSessions => Set<StickyNoteSession>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JiraIssue>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).IsRequired().HasMaxLength(50);
            e.Property(x => x.Summary).IsRequired().HasMaxLength(500);
            e.Property(x => x.BorderColor).HasDefaultValue("#2196F3");
        });

        modelBuilder.Entity<WorkNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.JiraIssue).WithMany(x => x.Notes).HasForeignKey(x => x.JiraIssueId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.WorkLog).WithMany(x => x.Notes).HasForeignKey(x => x.WorkLogId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
        });

        modelBuilder.Entity<WorkLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.JiraIssue).WithMany(x => x.WorkLogs).HasForeignKey(x => x.JiraIssueId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Status).HasConversion<string>();
        });

        modelBuilder.Entity<ChecklistItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.JiraIssue).WithMany(x => x.ChecklistItems).HasForeignKey(x => x.JiraIssueId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StickyNoteSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.JiraIssue).WithMany(x => x.Sessions).HasForeignKey(x => x.JiraIssueId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.SessionType).HasConversion<string>();
        });

        modelBuilder.Entity<AppSettings>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
        });
    }

    public static string GetDbPath()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JiraWorkLogAssistant");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "app.db");
    }
}
