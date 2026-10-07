# Jira WorkLog Assistant — User Manual

**Version 1.5.0**

A lightweight Windows desktop app that helps you capture work throughout the day as
sticky notes and turn them into professional Jira work logs using AI.

---

## 1. Overview

Instead of trying to remember everything at the end of the day, you jot quick notes as you
work. When you're ready, the app uses AI to polish those notes into proper Jira work logs and
submits them — either to a single task you pick, or automatically distributed across several
tasks ("Smart Distribution"). It runs quietly in the **system tray**.

**Key features**
- Sticky-note style capture windows (one color per note)
- AI-polished work logs (any OpenAI-compatible model)
- Manual (one task) or AI Smart Distribution (many tasks) modes
- Review screen before anything is submitted
- Optional automatic logging at a set time
- Dashboard with daily summary and scrum standup generator
- Works with **Jira Server / Data Center** (username + password)

---

## 2. Installation

1. Double-click **`JiraWorkLogAssistant-Setup-x.y.z.exe`**.
2. Follow the wizard (no administrator rights or pre-installed .NET required).
3. Optionally tick **Create a desktop shortcut** and **Start automatically when Windows starts**.
4. Click **Install**, then **Finish** (the app launches into the system tray).

> **Upgrading:** Just run the newer setup — it upgrades in place. **Exit the app first**
> (tray icon → Exit). Your settings and data are kept.
>
> **SmartScreen note:** Because the installer is unsigned, Windows may show
> "Windows protected your PC". Click **More info → Run anyway**.

---

## 3. First-Time Setup

Open **Settings** from the tray icon (right-click the tray icon → **Settings**).

### 3.1 Jira (Server / Data Center)
| Field | Example |
|---|---|
| **Jira Base URL** | `http://your-jira-server/jira` |
| **Username** | your Jira login |
| **Password** | your Jira password |

Click **Test Connection** — you should see *"Connected successfully!"* Then **Save**.

### 3.2 AI (any OpenAI-compatible endpoint)
| Field | Example |
|---|---|
| **API Base URL** | `https://api.openai.com/v1` (must end at `/v1`) |
| **API Key** | your key (leave blank for an unsecured local model) |
| **Model Name** | `gpt-4o-mini`, `openai/gpt-oss-120b`, etc. |

- The app always calls `<Base URL>/chat/completions` and sends the model in the request body.
- **If your endpoint encodes the model in the URL path**, paste the full base (e.g.
  `http://host:port/v2/gpt-oss-120b/v1`) and **leave Model Name blank** — the app then omits the
  model field and lets the URL select it.

Click **Test AI Connection**, then **Save**.

### 3.3 Sync your tasks
Tray icon → **Sync Jira**. This pulls your assigned, not-done issues.
Only **Task, Sub-task, Bug, and Improvement** types are loaded (others like CRS/SRS are ignored).

---

## 4. The System Tray Menu

Right-click the tray icon:

| Item | Action |
|---|---|
| **New Note** | Open a new capture window (also: double-click the tray icon) |
| **Jira Tasks** | Browse your synced issues; double-click one to open a note for it |
| **Dashboard** | Stats, daily summary, standup generator |
| **Sync Jira** | Refresh issues from Jira |
| **Settings** | Configure Jira, AI, Auto Log, General |
| **Exit** | Quit the application |

---

## 5. Capturing Work — The Note Window

Each note is a small, resizable, sticky-note-style window with its own color.
You can open **several at once**, each a different color.

### 5.1 Anatomy
- **Header:** the **Smart** toggle (left), the selected task (or a search box), minimize, close.
- **Formatting toolbar:** Bold, Italic, Underline, Strikethrough, bullet list, numbered list, A+/A− size.
- **Writing panel:** type freely — this is your note paper.
- **Footer:** **Hours/Minutes** time spinners and the **⚡ Log now** button.

### 5.2 Picking a task (Manual mode)
1. After syncing, a **search dropdown** appears below the header.
2. Type to filter by key or summary, then select a task.
3. The header collapses to **`KEY — Summary`** with a **✎ pencil**.
4. Click the **✎** any time to switch to a different task.

> A task open in one window is removed from other windows' dropdowns, so you can't log the
> same task in two places at once.

### 5.3 Writing notes
Type each thing you did. Use the toolbar to format (bold, bullets, etc.). Everything autosaves —
there is no Save button, and your text is restored if the app restarts.

### 5.4 Entering time
Set **Hours** and **Minutes** in the footer (type directly or use the ▲▼ arrows).

---

## 6. Logging Work to Jira

### 6.1 Manual logging (with review)
1. Write your notes, set the time, click **⚡ Log now**.
2. The AI **polishes** your notes into a professional work log.
3. A **review window** opens — edit the wording and time if needed.
4. Click **Submit to Jira**. On success the panel clears and you get a confirmation;
   on failure you'll see an error message (check the log).

### 6.2 Automatic logging
If **Auto Logging** is enabled in **Settings → Auto Log**:
- The **⚡ Log now** button is hidden (logging is automatic).
- At the configured **time**, each open task note is submitted **as written** (no AI rewrite,
  no review), preserving your formatting as Jira wiki markup. The note is then cleared.

---

## 7. AI Smart Distribution

Use this when you worked on **several tasks** but didn't track which note belongs to which.

1. In a note window, turn on the **Smart** toggle (the window turns purple). The panel starts empty.
2. Write all your notes freely, one per line.
3. Enter the **total time** you want split across tasks.
4. Click **⚡ Log now**. The AI:
   - matches each note to the best Jira task (Task/Sub-task/Bug/Improvement only),
   - polishes each note into a **bullet point** under its task,
   - splits your total time across the tasks by estimated effort.
5. A **review window** opens listing each task with:
   - confidence %, the editable bulleted work log, and an editable Hours/Minutes,
   - a **✕** button to drop any task you don't want to log,
   - a running **Allocated / target** total (turns green when they match).
6. Click **Submit to Jira**. Low-confidence matches are flagged so you can double-check.

---

## 8. Dashboard

Tray → **Dashboard**:
- **Today's stats** — tasks, hours logged, pending notes.
- **Generate Daily Summary** — an AI paragraph of today's accomplishments.
- **Generate Standup** — an AI scrum stand-up built from your recent work logs and open notes:
  ```
  Update:
  - ...
  Blockers:
  - ...
  Risks:
  - ...
  ```
  Blockers and risks are inferred from your work (or shown as "None").
- **Active issues** and recent submission history.

---

## 9. Settings Reference

**Jira** — Base URL, Username, Password, Test Connection.
**AI** — API Base URL, API Key, Model Name, Test AI Connection.

**Auto Log**
- **Enable Auto Logging** (off by default) — turns on automatic, as-is logging.
- **Daily Logging Time (HH:mm)** — when auto-logging runs.

**General**
- **Launch on Windows Startup**
- **Submission Behavior** — Require Approval, Archive Notes, Close Windows, Enable Daily Summary
- **Notifications** — Show Idle Notification + idle minutes
- **Smart Distribution** — Minimum match confidence

Changes save immediately and apply to open windows (e.g. enabling Auto Logging hides "Log now").

---

## 10. Formatting in Jira

The editor supports **bold, italic, underline, strikethrough, bullets, and numbered lists**.
When a note is submitted **as-is** (auto-logging) the formatting is converted to **Jira wiki
markup** (`*bold*`, `_italic_`, `* bullet`, `# numbered`, etc.) so it renders in the worklog.

> Manual and Smart logging submit the **AI-polished** text, so your typed formatting is
> replaced by the AI's clean wording (you can still add wiki markup in the review screen).

---

## 11. Where Your Data Lives

All local data is stored at:
```
%LocalAppData%\JiraWorkLogAssistant\
   app.db        (SQLite database: tasks, notes, work logs, settings)
   logs\         (diagnostic logs)
```
Jira and AI credentials are **encrypted**. This folder is removed if you **uninstall** the app.

---

## 12. Troubleshooting

| Problem | What to check |
|---|---|
| **Jira "Connection failed"** | Base URL ends at `/jira` (your context path), correct username/password. Server/DC uses API v2 (the app handles this). Check `logs\` for the exact HTTP error (401 = auth, 404 = wrong URL). |
| **AI fails but Jira works** | API Base URL must end at `/v1`; the app appends `/chat/completions`. If the model is in the URL path, leave Model Name blank. |
| **CRS/SRS still showing** | **Sync Jira** once after upgrading — old issues get deactivated. |
| **Submit does nothing** | Update to the latest version. Check `logs\` for the error; verify time and a non-empty work log. |
| **No standup details** | Generate it after you've logged some work today; it uses recent work logs + open notes. |
| **App not visible** | It runs in the tray — click the **^** chevron near the clock; double-click the icon for a new note. |

To collect logs for support: open `%LocalAppData%\JiraWorkLogAssistant\logs\` and send the newest file.

---

## 13. Uninstalling

**Settings → Apps → Jira WorkLog Assistant → Uninstall** (or via Add/Remove Programs).
This removes the app and its local data folder.

---

*Jira WorkLog Assistant — capture work as it happens, log it effortlessly.*
