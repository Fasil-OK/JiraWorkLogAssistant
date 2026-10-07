"""Build the one-page Jira WorkLog Assistant overview as a PDF."""
import sys
from reportlab.lib.pagesizes import LETTER
from reportlab.lib.units import inch
from reportlab.lib.colors import HexColor
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_CENTER
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, HRFlowable, ListFlowable, ListItem, Table, TableStyle
)

ACCENT = HexColor("#5B21B6")
BLUE = HexColor("#2563EB")
DARK = HexColor("#1F2937")
GREY = HexColor("#6B7280")
LIGHT = HexColor("#F3F4F6")

out = sys.argv[1] if len(sys.argv) > 1 else "Overview.pdf"

doc = SimpleDocTemplate(
    out, pagesize=LETTER,
    leftMargin=0.8 * inch, rightMargin=0.8 * inch,
    topMargin=0.7 * inch, bottomMargin=0.5 * inch,
    title="Jira WorkLog Assistant — Overview",
    author="Jira WorkLog Assistant",
)

title = ParagraphStyle("title", fontName="Helvetica-Bold", fontSize=25, leading=28, textColor=ACCENT, spaceAfter=2)
tag = ParagraphStyle("tag", fontName="Helvetica-Oblique", fontSize=11.8, leading=15, textColor=GREY, spaceAfter=6)
body = ParagraphStyle("body", fontName="Helvetica", fontSize=10.3, leading=14.4, textColor=DARK, spaceAfter=6)
head = ParagraphStyle("head", fontName="Helvetica-Bold", fontSize=12.5, leading=15.5, textColor=ACCENT,
                      spaceBefore=10, spaceAfter=5)
head_blue = ParagraphStyle("head_blue", parent=head, textColor=BLUE)
bullet = ParagraphStyle("bullet", parent=body, fontSize=10.3, leading=14, spaceAfter=2.5)
steps = ParagraphStyle("steps", parent=body, fontSize=10.8, leading=15, spaceAfter=2)
closing = ParagraphStyle("closing", fontName="Helvetica-BoldOblique", fontSize=13.5, leading=17,
                         textColor=ACCENT, alignment=TA_CENTER, spaceBefore=8)


def blist(items):
    return ListFlowable(
        [ListItem(Paragraph(t, bullet), leftIndent=16, value="square") for t in items],
        bulletType="bullet", bulletFontSize=6, bulletColor=BLUE,
        leftIndent=14, spaceAfter=3,
    )


story = []
story.append(Paragraph("Jira WorkLog Assistant", title))
story.append(Paragraph("Capture work as it happens. Let AI write your Jira work logs.", tag))
story.append(HRFlowable(width="100%", thickness=1.5, color=ACCENT, spaceAfter=12))

story.append(Paragraph(
    "<b>The problem.</b> By the end of the day you can't remember exactly what you did — so Jira "
    "work logs get rushed, vague, or skipped altogether.", body))
story.append(Paragraph(
    "<b>The solution.</b> A lightweight sticky-note app that lives in your system tray. Jot one line "
    "whenever you finish something. When you're ready, AI turns those notes into clean, professional "
    "Jira work logs and submits them for you.", body))

story.append(Paragraph("What it does", head))
story.append(blist([
    "<b>Sticky notes that stay out of your way</b> — one colour per task, always a click away in the tray.",
    "<b>AI-polished work logs</b> — your raw, terse notes become clear, point-by-point entries.",
    "<b>Smart Distribution</b> — worked on five tasks? Write freely; AI matches each note to the right Jira issue and splits your hours.",
    "<b>Pull from Bitbucket</b> — one click imports today's commit messages and summarises them as notes.",
    "<b>Auto-log</b> — set a time and your notes are submitted to Jira automatically.",
    "<b>Review before submit</b> — nothing reaches Jira until you approve it.",
    "<b>Daily summary &amp; scrum standup</b> — Update / Blockers / Risks, generated for you.",
]))

story.append(Paragraph("Why you'll love it", head_blue))
story.append(blist([
    "<b>Get 15–20 minutes back, every day.</b> No more end-of-day archaeology.",
    "<b>Logs that are actually accurate</b> — written while the work is fresh, backed by your real commits.",
    "<b>Look good in standups and reviews</b> — your effort is visible, and well worded.",
    "<b>Private by design</b> — your data stays on your PC; all credentials are encrypted.",
    "<b>Use any AI</b> — OpenAI, or your company's self-hosted model. Nothing has to leave your network.",
    "<b>Zero friction</b> — installs in seconds. No admin rights. No .NET to install.",
]))

# "Works with" callout
callout = Paragraph(
    "<b>Works with</b> &nbsp; Jira Server / Data Center &nbsp;·&nbsp; Bitbucket Data Center "
    "&nbsp;·&nbsp; any OpenAI-compatible AI endpoint", body)
t = Table([[callout]], colWidths=[doc.width])
t.setStyle(TableStyle([
    ("BACKGROUND", (0, 0), (-1, -1), LIGHT),
    ("LINEBEFORE", (0, 0), (0, -1), 3, BLUE),
    ("LEFTPADDING", (0, 0), (-1, -1), 8),
    ("RIGHTPADDING", (0, 0), (-1, -1), 6),
    ("TOPPADDING", (0, 0), (-1, -1), 6),
    ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
]))
story.append(Spacer(1, 5))
story.append(t)

story.append(Paragraph("Get started in 3 steps", head))
story.append(Paragraph(
    "<b>1. Install</b> <font color='#6B7280'>(one double-click)</font> &nbsp;&nbsp; "
    "<b>2. Connect</b> <font color='#6B7280'>Jira, Bitbucket &amp; AI</font> &nbsp;&nbsp; "
    "<b>3. Start jotting</b>", steps))

story.append(Spacer(1, 4))
story.append(HRFlowable(width="100%", thickness=1.2, color=ACCENT, spaceAfter=4))
story.append(Paragraph("Stop remembering. Start logging.", closing))

doc.build(story)
print("WROTE", out)
