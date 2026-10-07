const fs = require("fs");
const {
  Document, Packer, Paragraph, TextRun, AlignmentType, LevelFormat,
  BorderStyle, ShadingType, HeadingLevel
} = require("docx");

const ACCENT = "5B21B6";
const BLUE = "2563EB";
const DARK = "1F2937";
const GREY = "6B7280";

// inline **bold** parsing
function runs(text, opts = {}) {
  const out = [];
  const re = /(\*\*[^*]+\*\*)/g;
  let last = 0, m;
  while ((m = re.exec(text)) !== null) {
    if (m.index > last) out.push(new TextRun({ text: text.slice(last, m.index), ...opts }));
    out.push(new TextRun({ text: m[0].slice(2, -2), bold: true, ...opts }));
    last = re.lastIndex;
  }
  if (last < text.length) out.push(new TextRun({ text: text.slice(last), ...opts }));
  return out.length ? out : [new TextRun({ text, ...opts })];
}

const bullet = (t) => new Paragraph({
  numbering: { reference: "b", level: 0 },
  spacing: { after: 20 },
  children: runs(t, { size: 19 })
});

const heading = (t, color = ACCENT) => new Paragraph({
  spacing: { before: 140, after: 60 },
  children: [new TextRun({ text: t, bold: true, size: 22, color })]
});

const body = (t, opts = {}) => new Paragraph({
  spacing: { after: 60 },
  children: runs(t, { size: 19, ...opts })
});

const children = [
  // ---- Header block ----
  new Paragraph({
    spacing: { after: 0 },
    children: [new TextRun({ text: "Jira WorkLog Assistant", bold: true, size: 40, color: ACCENT })]
  }),
  new Paragraph({
    spacing: { after: 40 },
    children: [new TextRun({ text: "Capture work as it happens. Let AI write your Jira work logs.", italics: true, size: 21, color: GREY })]
  }),
  new Paragraph({
    spacing: { after: 120 },
    border: { bottom: { style: BorderStyle.SINGLE, size: 8, color: ACCENT, space: 2 } },
    children: [new TextRun({ text: "", size: 2 })]
  }),

  // ---- Problem / Solution ----
  body("**The problem.** By the end of the day you can't remember exactly what you did — so Jira work logs get rushed, vague, or skipped altogether."),
  body("**The solution.** A lightweight sticky-note app that lives in your system tray. Jot one line whenever you finish something. When you're ready, AI turns those notes into clean, professional Jira work logs and submits them for you."),

  // ---- Features ----
  heading("What it does"),
  bullet("**Sticky notes that stay out of your way** — one colour per task, always a click away in the tray."),
  bullet("**AI-polished work logs** — your raw, terse notes become clear, point-by-point entries."),
  bullet("**Smart Distribution** — worked on five tasks? Write freely; AI matches each note to the right Jira issue and splits your hours."),
  bullet("**Pull from Bitbucket** — one click imports today's commit messages and summarises them as notes."),
  bullet("**Auto-log** — set a time and your notes are submitted to Jira automatically."),
  bullet("**Review before submit** — nothing reaches Jira until you approve it."),
  bullet("**Daily summary & scrum standup** — Update / Blockers / Risks, generated for you."),

  // ---- Advantages ----
  heading("Why you'll love it", BLUE),
  bullet("**Get 15–20 minutes back, every day.** No more end-of-day archaeology."),
  bullet("**Logs that are actually accurate** — written while the work is fresh, backed by your real commits."),
  bullet("**Look good in standups and reviews** — your effort is visible, and well worded."),
  bullet("**Private by design** — your data stays on your PC; all credentials are encrypted."),
  bullet("**Use any AI** — OpenAI, or your company's self-hosted model. Nothing has to leave your network."),
  bullet("**Zero friction** — installs in seconds. No admin rights. No .NET to install."),

  // ---- Works with ----
  new Paragraph({
    spacing: { before: 140, after: 100 },
    shading: { fill: "F3F4F6", type: ShadingType.CLEAR, color: "auto" },
    border: { left: { style: BorderStyle.SINGLE, size: 18, color: BLUE, space: 6 } },
    children: [
      new TextRun({ text: "Works with   ", bold: true, size: 19, color: DARK }),
      new TextRun({ text: "Jira Server / Data Center   ·   Bitbucket Data Center   ·   any OpenAI-compatible AI endpoint", size: 19, color: DARK })
    ]
  }),

  // ---- Get started ----
  heading("Get started in 3 steps"),
  new Paragraph({
    spacing: { after: 120 },
    children: [
      new TextRun({ text: "1.  Install  ", bold: true, size: 19 }),
      new TextRun({ text: "(one double-click)     ", size: 19, color: GREY }),
      new TextRun({ text: "2.  Connect  ", bold: true, size: 19 }),
      new TextRun({ text: "Jira, Bitbucket & AI     ", size: 19, color: GREY }),
      new TextRun({ text: "3.  Start jotting", bold: true, size: 19 }),
    ]
  }),

  // ---- Closing ----
  new Paragraph({
    spacing: { before: 60 },
    alignment: AlignmentType.CENTER,
    border: { top: { style: BorderStyle.SINGLE, size: 8, color: ACCENT, space: 6 } },
    children: [new TextRun({ text: "Stop remembering. Start logging.", bold: true, italics: true, size: 22, color: ACCENT })]
  }),
];

const doc = new Document({
  styles: { default: { document: { run: { font: "Arial", size: 19, color: DARK } } } },
  numbering: {
    config: [{
      reference: "b",
      levels: [{
        level: 0, format: LevelFormat.BULLET, text: "▪",
        alignment: AlignmentType.LEFT,
        style: { paragraph: { indent: { left: 360, hanging: 220 } } }
      }]
    }]
  },
  sections: [{
    properties: {
      page: {
        size: { width: 12240, height: 15840 },
        margin: { top: 900, right: 1000, bottom: 700, left: 1000 }
      }
    },
    children
  }]
});

Packer.toBuffer(doc).then(buf => {
  fs.writeFileSync(process.argv[2] || "OnePager.docx", buf);
  console.log("WROTE", process.argv[2]);
});
