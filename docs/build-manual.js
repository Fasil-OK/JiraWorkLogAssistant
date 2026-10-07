const fs = require("fs");
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
  AlignmentType, LevelFormat, HeadingLevel, BorderStyle, WidthType, ShadingType,
  TableOfContents, PageBreak, Header, Footer, PageNumber, VerticalAlign
} = require("docx");

const ACCENT = "5B21B6";   // purple
const DARK = "1F2937";
const GREY = "6B7280";
const CONTENT_W = 9360;     // US Letter, 1" margins

// ---------- helpers ----------
const H1 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(t)] });
const H2 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(t)] });
const P = (t, opts = {}) => new Paragraph({ spacing: { after: 120 }, children: [new TextRun({ text: t, ...opts })] });
const bullet = (t) => new Paragraph({ numbering: { reference: "bullets", level: 0 }, spacing: { after: 40 }, children: runs(t) });
const num = (t) => new Paragraph({ numbering: { reference: "steps", level: 0 }, spacing: { after: 40 }, children: runs(t) });

// inline **bold** and `code`
function runs(text) {
  const out = [];
  const re = /(\*\*[^*]+\*\*|`[^`]+`)/g;
  let last = 0, m;
  while ((m = re.exec(text)) !== null) {
    if (m.index > last) out.push(new TextRun(text.slice(last, m.index)));
    const tok = m[0];
    if (tok.startsWith("**")) out.push(new TextRun({ text: tok.slice(2, -2), bold: true }));
    else out.push(new TextRun({ text: tok.slice(1, -1), font: "Consolas", color: ACCENT }));
    last = re.lastIndex;
  }
  if (last < text.length) out.push(new TextRun(text.slice(last)));
  return out.length ? out : [new TextRun(text)];
}

const border = { style: BorderStyle.SINGLE, size: 1, color: "CCCCCC" };
const borders = { top: border, bottom: border, left: border, right: border,
  insideHorizontal: border, insideVertical: border };

function table(headers, rows, widths) {
  const cell = (text, w, opts = {}) => new TableCell({
    width: { size: w, type: WidthType.DXA },
    margins: { top: 60, bottom: 60, left: 120, right: 120 },
    shading: opts.head ? { fill: ACCENT, type: ShadingType.CLEAR, color: "auto" } : undefined,
    verticalAlign: VerticalAlign.CENTER,
    children: [new Paragraph({ spacing: { after: 0 },
      children: runs(text).map(r => { if (opts.head) { r.bold = true; } return r; })
        .map(r => opts.head ? new TextRun({ text: r.text ?? "", bold: true, color: "FFFFFF" }) : r) })]
  });
  const headRow = new TableRow({ tableHeader: true,
    children: headers.map((h, i) => cell(h, widths[i], { head: true })) });
  const bodyRows = rows.map(r => new TableRow({ children: r.map((c, i) => cell(c, widths[i])) }));
  return new Table({ width: { size: CONTENT_W, type: WidthType.DXA }, columnWidths: widths,
    borders, rows: [headRow, ...bodyRows] });
}

function codeBlock(lines) {
  return new Paragraph({
    spacing: { before: 60, after: 120 },
    shading: { fill: "F3F4F6", type: ShadingType.CLEAR, color: "auto" },
    border: { top: { style: BorderStyle.SINGLE, size: 4, color: "E5E7EB" },
              bottom: { style: BorderStyle.SINGLE, size: 4, color: "E5E7EB" },
              left: { style: BorderStyle.SINGLE, size: 4, color: "E5E7EB" },
              right: { style: BorderStyle.SINGLE, size: 4, color: "E5E7EB" } },
    children: lines.flatMap((l, i) => i === 0 ? [new TextRun({ text: l, font: "Consolas", size: 18 })]
      : [new TextRun({ break: 1, text: l, font: "Consolas", size: 18 })])
  });
}

function note(text) {
  return new Paragraph({
    spacing: { before: 60, after: 120 },
    indent: { left: 240 },
    border: { left: { style: BorderStyle.SINGLE, size: 18, color: ACCENT, space: 8 } },
    children: [new TextRun({ text: "Note  ", bold: true, color: ACCENT }), ...runs(text)]
  });
}

// ---------- document ----------
const children = [];

// Title page
children.push(
  new Paragraph({ spacing: { before: 2600, after: 0 }, alignment: AlignmentType.CENTER,
    children: [new TextRun({ text: "Jira WorkLog Assistant", bold: true, size: 56, color: ACCENT })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 120, after: 0 },
    children: [new TextRun({ text: "User Manual", size: 36, color: DARK })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 200 },
    children: [new TextRun({ text: "Version 1.5.0", size: 24, color: GREY })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 1200 },
    children: [new TextRun({ text: "Capture work as it happens. Log it effortlessly.", italics: true, size: 24, color: GREY })] }),
  new Paragraph({ children: [new PageBreak()] }),
);

// TOC
children.push(
  new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun("Contents")] }),
  new TableOfContents("Contents", { hyperlink: true, headingStyleRange: "1-2" }),
  new Paragraph({ children: [new PageBreak()] }),
);

// 1. Overview
children.push(H1("1. Overview"));
children.push(P("Instead of trying to remember everything at the end of the day, you jot quick notes as you work. When you are ready, the app uses AI to polish those notes into proper Jira work logs and submits them — either to a single task you pick, or automatically distributed across several tasks (“Smart Distribution”). It runs quietly in the Windows system tray."));
children.push(H2("Key features"));
["Sticky-note style capture windows (one color per note)",
 "AI-polished work logs (any OpenAI-compatible model)",
 "Manual (one task) or AI Smart Distribution (many tasks) modes",
 "A review screen before anything is submitted",
 "Optional automatic logging at a set time",
 "Dashboard with daily summary and a scrum standup generator",
 "Works with Jira Server / Data Center using username and password"].forEach(t => children.push(bullet(t)));

// 2. Installation
children.push(H1("2. Installation"));
[ "Double-click **JiraWorkLogAssistant-Setup-x.y.z.exe**.",
  "Follow the wizard (no administrator rights or pre-installed .NET required).",
  "Optionally tick **Create a desktop shortcut** and **Start automatically when Windows starts**.",
  "Click **Install**, then **Finish** (the app launches into the system tray)."].forEach(t => children.push(num(t)));
children.push(note("Upgrading: run the newer setup — it upgrades in place. Exit the app first (tray icon → Exit). Your settings and data are kept."));
children.push(note("SmartScreen: because the installer is unsigned, Windows may show “Windows protected your PC”. Click More info → Run anyway."));

// 3. First-time setup
children.push(H1("3. First-Time Setup"));
children.push(P("Open Settings from the tray icon (right-click the tray icon → Settings)."));
children.push(H2("3.1 Jira (Server / Data Center)"));
children.push(table(["Field", "Example"], [
  ["Jira Base URL", "http://your-jira-server/jira"],
  ["Username", "your Jira login"],
  ["Password", "your Jira password"],
], [3000, 6360]));
children.push(P("Click Test Connection — you should see “Connected successfully!” Then Save."));
children.push(H2("3.2 AI (any OpenAI-compatible endpoint)"));
children.push(table(["Field", "Example"], [
  ["API Base URL", "https://api.openai.com/v1  (must end at /v1)"],
  ["API Key", "your key (leave blank for an unsecured local model)"],
  ["Model Name", "gpt-4o-mini, openai/gpt-oss-120b, etc."],
], [3000, 6360]));
children.push(bullet("The app always calls `<Base URL>/chat/completions` and sends the model in the request body."));
children.push(bullet("If your endpoint encodes the model in the URL path, paste the full base (e.g. `http://host:port/v2/gpt-oss-120b/v1`) and leave Model Name blank — the app then omits the model field and lets the URL select it."));
children.push(P("Click Test AI Connection, then Save.", { italics: false }));
children.push(H2("3.3 Sync your tasks"));
children.push(P("Tray icon → Sync Jira. This pulls your assigned, not-done issues. Only Task, Sub-task, Bug, and Improvement types are loaded (others like CRS / SRS are ignored)."));

// 4. Tray menu
children.push(H1("4. The System Tray Menu"));
children.push(P("Right-click the tray icon:"));
children.push(table(["Item", "Action"], [
  ["New Note", "Open a new capture window (also: double-click the tray icon)"],
  ["Jira Tasks", "Browse your synced issues; double-click one to open a note for it"],
  ["Dashboard", "Stats, daily summary, standup generator"],
  ["Sync Jira", "Refresh issues from Jira"],
  ["Settings", "Configure Jira, AI, Auto Log, General"],
  ["Exit", "Quit the application"],
], [2400, 6960]));

// 5. Note window
children.push(H1("5. Capturing Work — The Note Window"));
children.push(P("Each note is a small, resizable, sticky-note-style window with its own color. You can open several at once, each a different color."));
children.push(H2("5.1 Anatomy"));
["**Header:** the Smart toggle (left), the selected task (or a search box), minimize, close.",
 "**Formatting toolbar:** Bold, Italic, Underline, Strikethrough, bullet list, numbered list, A+/A− size.",
 "**Writing panel:** type freely — this is your note paper.",
 "**Footer:** Hours/Minutes time spinners and the Log now button."].forEach(t => children.push(bullet(t)));
children.push(H2("5.2 Picking a task (Manual mode)"));
["After syncing, a search dropdown appears below the header.",
 "Type to filter by key or summary, then select a task.",
 "The header collapses to **KEY — Summary** with a pencil (✎) icon.",
 "Click the pencil any time to switch to a different task."].forEach(t => children.push(num(t)));
children.push(note("A task open in one window is removed from other windows’ dropdowns, so you cannot log the same task in two places at once."));
children.push(H2("5.3 Writing notes & time"));
children.push(P("Type each thing you did and format it with the toolbar. Everything autosaves — there is no Save button, and your text is restored if the app restarts. Set Hours and Minutes in the footer (type directly or use the up/down arrows)."));

// 6. Logging
children.push(H1("6. Logging Work to Jira"));
children.push(H2("6.1 Manual logging (with review)"));
["Write your notes, set the time, and click **Log now**.",
 "The AI polishes your notes into a professional work log.",
 "A review window opens — edit the wording and time if needed.",
 "Click **Submit to Jira**. On success the panel clears and you get a confirmation; on failure you see an error message (check the log)."].forEach(t => children.push(num(t)));
children.push(H2("6.2 Automatic logging"));
children.push(P("If Auto Logging is enabled in Settings → Auto Log:"));
["The Log now button is hidden (logging is automatic).",
 "At the configured time, each open task note is submitted as written (no AI rewrite, no review), preserving your formatting as Jira wiki markup. The note is then cleared."].forEach(t => children.push(bullet(t)));

// 7. Smart distribution
children.push(H1("7. AI Smart Distribution"));
children.push(P("Use this when you worked on several tasks but did not track which note belongs to which."));
["In a note window, turn on the **Smart** toggle (the window turns purple). The panel starts empty.",
 "Write all your notes freely, one per line.",
 "Enter the total time you want split across tasks.",
 "Click **Log now**. The AI matches each note to the best Jira task, polishes each note into a bullet point under its task, and splits your total time by estimated effort.",
 "A review window lists each task with confidence %, the editable bulleted work log, an editable time, and a ✕ button to drop a task. A running Allocated / target total turns green when they match.",
 "Click **Submit to Jira**. Low-confidence matches are flagged so you can double-check."].forEach(t => children.push(num(t)));

// 8. Dashboard
children.push(H1("8. Dashboard"));
children.push(P("Tray → Dashboard:"));
["**Today’s stats** — tasks, hours logged, pending notes.",
 "**Generate Daily Summary** — an AI paragraph of today’s accomplishments.",
 "**Generate Standup** — an AI scrum stand-up built from your recent work logs and open notes, with Update / Blockers / Risks headings. Blockers and risks are inferred from your work (or shown as “None”).",
 "**Active issues** and recent submission history."].forEach(t => children.push(bullet(t)));
children.push(codeBlock(["Update:", "- ...", "Blockers:", "- ...", "Risks:", "- ..."]));

// 9. Settings reference
children.push(H1("9. Settings Reference"));
children.push(table(["Tab", "Settings"], [
  ["Jira", "Base URL, Username, Password, Test Connection"],
  ["AI", "API Base URL, API Key, Model Name, Test AI Connection"],
  ["Auto Log", "Enable Auto Logging (off by default); Daily Logging Time (HH:mm)"],
  ["General", "Launch on Windows Startup; Require Approval / Archive Notes / Close Windows / Daily Summary; Idle notifications; Smart Distribution minimum confidence"],
], [1800, 7560]));
children.push(note("Changes save immediately and apply to open windows (for example, enabling Auto Logging hides the Log now button)."));

// 10. Formatting
children.push(H1("10. Formatting in Jira"));
children.push(P("The editor supports bold, italic, underline, strikethrough, bullets, and numbered lists. When a note is submitted as-is (auto-logging) the formatting is converted to Jira wiki markup (`*bold*`, `_italic_`, `* bullet`, `# numbered`) so it renders in the worklog."));
children.push(note("Manual and Smart logging submit the AI-polished text, so your typed formatting is replaced by the AI’s clean wording. You can still add wiki markup in the review screen."));

// 11. Data
children.push(H1("11. Where Your Data Lives"));
children.push(P("All local data is stored under your user profile:"));
children.push(codeBlock(["%LocalAppData%\\JiraWorkLogAssistant\\",
  "   app.db        (SQLite: tasks, notes, work logs, settings)",
  "   logs\\         (diagnostic logs)"]));
children.push(P("Jira and AI credentials are stored encrypted. This folder is removed if you uninstall the app."));

// 12. Troubleshooting
children.push(H1("12. Troubleshooting"));
children.push(table(["Problem", "What to check"], [
  ["Jira “Connection failed”", "Base URL includes your context path (e.g. /jira); correct username/password. Check logs for the HTTP error (401 = auth, 404 = wrong URL)."],
  ["AI fails but Jira works", "API Base URL must end at /v1; the app appends /chat/completions. If the model is in the URL path, leave Model Name blank."],
  ["CRS/SRS still showing", "Run Sync Jira once after upgrading — old issues get deactivated."],
  ["Submit does nothing", "Update to the latest version. Check logs; verify time and a non-empty work log."],
  ["No standup details", "Generate it after you have logged some work today."],
  ["App not visible", "It runs in the tray — click the ^ chevron near the clock; double-click the icon for a new note."],
], [3000, 6360]));
children.push(P("To collect logs for support, open %LocalAppData%\\JiraWorkLogAssistant\\logs\\ and send the newest file."));

// 13. Uninstall
children.push(H1("13. Uninstalling"));
children.push(P("Settings → Apps → Jira WorkLog Assistant → Uninstall (or via Add/Remove Programs). This removes the app and its local data folder."));

// ---------- assemble ----------
const doc = new Document({
  styles: {
    default: { document: { run: { font: "Arial", size: 22, color: DARK } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 30, bold: true, color: ACCENT, font: "Arial" },
        paragraph: { spacing: { before: 320, after: 160 }, outlineLevel: 0 } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 25, bold: true, color: DARK, font: "Arial" },
        paragraph: { spacing: { before: 200, after: 100 }, outlineLevel: 1 } },
    ]
  },
  numbering: {
    config: [
      { reference: "bullets", levels: [{ level: 0, format: LevelFormat.BULLET, text: "•",
        alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 540, hanging: 280 } } } }] },
      { reference: "steps", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.",
        alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 540, hanging: 280 } } } }] },
    ]
  },
  sections: [{
    properties: { page: { size: { width: 12240, height: 15840 },
      margin: { top: 1440, right: 1440, bottom: 1440, left: 1440 } } },
    headers: { default: new Header({ children: [new Paragraph({ alignment: AlignmentType.RIGHT,
      border: { bottom: { style: BorderStyle.SINGLE, size: 4, color: "E5E7EB", space: 4 } },
      children: [new TextRun({ text: "Jira WorkLog Assistant — User Manual", size: 16, color: GREY })] })] }) },
    footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER,
      children: [new TextRun({ text: "Page ", size: 16, color: GREY }),
        new TextRun({ children: [PageNumber.CURRENT], size: 16, color: GREY })] })] }) },
    children
  }]
});

Packer.toBuffer(doc).then(buf => {
  fs.writeFileSync(process.argv[2] || "UserManual.docx", buf);
  console.log("WROTE", process.argv[2]);
});
