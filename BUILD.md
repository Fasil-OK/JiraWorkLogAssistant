# Jira WorkLog Assistant — Build Instructions

## Prerequisites

1. **.NET 8 SDK** — Download from https://dotnet.microsoft.com/download/dotnet/8.0
   - Install **SDK** (not just Runtime)
   - Verify: `dotnet --version` (should print 8.x.x)

2. **Windows 10/11** (required — WPF is Windows-only)

3. **Git** (optional, for git integration feature)

---

## Quick Start

```powershell
# 1. Clone or open the project folder
cd C:\Users\admin\Desktop\worklog_assist\JiraWorkLogAssistant

# 2. Restore NuGet packages
dotnet restore src\JiraWorkLogAssistant\JiraWorkLogAssistant.csproj

# 3. Build (Debug)
dotnet build src\JiraWorkLogAssistant\JiraWorkLogAssistant.csproj

# 4. Run
dotnet run --project src\JiraWorkLogAssistant\JiraWorkLogAssistant.csproj
```

---

## First Run

On first startup:
1. App icon appears in the **system tray** (bottom-right notification area)
2. Right-click tray icon → **Settings** to configure Jira & AI credentials
3. Right-click tray icon → **Sync Jira** to load your issues
4. Right-click tray icon → **Open Jira Tasks** → double-click any issue to open a sticky note

---

## Publish (Release / Single EXE)

```powershell
dotnet publish src\JiraWorkLogAssistant\JiraWorkLogAssistant.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o publish\

# The executable will be at:
# publish\JiraWorkLogAssistant.exe
```

### Self-contained (no .NET required on target machine):
```powershell
dotnet publish src\JiraWorkLogAssistant\JiraWorkLogAssistant.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish-sc\
```

---

## Database

SQLite database is stored at:
```
%LOCALAPPDATA%\JiraWorkLogAssistant\app.db
```

The database is created automatically on first run (EF Core `EnsureCreated`).

---

## Windows Startup (Auto-launch)

In Settings → Auto Log tab → enable "Launch on Windows Startup".

This writes to:
`HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`

---

## Installer (Inno Setup)

Install Inno Setup from https://jrsoftware.org/isinfo.php, then publish the
self-contained build to `publish\app\` (see above) and compile:

```
iscc installer\JiraWorkLogAssistant.iss
```

The signed-per-user installer is written to `publish\installer\`.
See `installer\JiraWorkLogAssistant.iss` for the installer script.

---

## Project Structure

```
src\JiraWorkLogAssistant\
├── Core\
│   ├── Models\          — EF entity models
│   ├── Data\            — DbContext + Migrations + Repositories
│   └── Services\        — Jira, AI, Settings, AutoLogging, Git, Encryption
├── ViewModels\          — MVVM ViewModels (CommunityToolkit.Mvvm)
├── Views\               — WPF Windows + code-behind
│   └── Converters\      — Value converters
└── Resources\
    └── Styles\          — App-wide XAML styles
```
