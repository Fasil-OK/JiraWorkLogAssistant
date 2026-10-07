# Contributing to Jira WorkLog Assistant

Thanks for your interest in improving the project! Contributions of all kinds are welcome — bug reports, feature ideas, docs, and code.

## Ways to contribute

- **Report a bug** — open a [bug report](../../issues/new?template=bug_report.yml). Include your app version, Windows version, and sanitized logs.
- **Request a feature** — open a [feature request](../../issues/new?template=feature_request.yml).
- **Improve the docs** — the user guide lives in [`docs/`](docs/).
- **Submit a fix** — see below.

## Development setup

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Windows 10/11 (WPF is Windows-only).

```powershell
git clone https://github.com/Fasil-OK/JiraWorkLogAssistant.git
cd JiraWorkLogAssistant
dotnet restore src/JiraWorkLogAssistant/JiraWorkLogAssistant.csproj
dotnet run   --project src/JiraWorkLogAssistant/JiraWorkLogAssistant.csproj
```

See [BUILD.md](BUILD.md) for publishing and installer steps.

## Pull requests

1. Fork the repo and create a branch from `main` (e.g. `fix/jira-timezone`).
2. Keep changes focused — one logical change per PR.
3. Match the existing code style (MVVM, `CommunityToolkit.Mvvm`, nullable enabled). New code should read like the code around it.
4. Make sure it builds in Release: `dotnet build src/JiraWorkLogAssistant/JiraWorkLogAssistant.csproj -c Release`.
5. Describe **what** changed and **why** in the PR description. Screenshots help for UI changes.

## Architecture at a glance

| Layer | Folder | Notes |
|-------|--------|-------|
| Models | `Core/Models` | EF Core entities + settings records |
| Data | `Core/Data` | `AppDbContext` (SQLite, `EnsureCreated`), repositories |
| Services | `Core/Services` | Jira, Bitbucket, AI, Settings, AutoLogging, Encryption |
| ViewModels | `ViewModels` | MVVM, dependency-injected |
| Views | `Views` | WPF windows + tray icon |

All external calls (Jira, Bitbucket, AI) go through a service interface — mock the interface to test a ViewModel in isolation.

## Code of conduct

Be kind and constructive. We're all here to build something useful.
