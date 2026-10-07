using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace JiraWorkLogAssistant.Core.Services;

public class GitIntegrationService
{
    private readonly ILogger<GitIntegrationService> _logger;

    public GitIntegrationService(ILogger<GitIntegrationService> logger)
    {
        _logger = logger;
    }

    public async Task<List<string>> GetRecentCommitsAsync(string repoPath, int count = 20)
    {
        try
        {
            var result = await RunGitAsync(repoPath, $"log --oneline -n {count}");
            return result.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        catch { return new(); }
    }

    public async Task<string?> GetCurrentBranchAsync(string repoPath)
    {
        try { return (await RunGitAsync(repoPath, "branch --show-current")).Trim(); }
        catch { return null; }
    }

    public List<string> ExtractJiraKeys(IEnumerable<string> gitMessages)
    {
        var pattern = new Regex(@"[A-Z]+-\d+", RegexOptions.IgnoreCase);
        return gitMessages
            .SelectMany(m => pattern.Matches(m).Select(x => x.Value.ToUpper()))
            .Distinct()
            .ToList();
    }

    private async Task<string> RunGitAsync(string repoPath, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return output;
    }
}
