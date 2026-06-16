// Cyberpunk-themed workshop deck for CustomAgentHarness on Agent 365
// Output: C:\customagentharness\workshop\slides.pptx
//
// Palette (no '#' prefix per pptxgenjs):
//   BG_DEEP    0A0F1F  - slide background
//   BG_PANEL   141828  - cards / panels
//   BG_CODE    0D1224  - terminal blocks
//   TEXT       F0F2FA  - primary text
//   MUTED      8A8FA3  - secondary
//   ORANGE     FF8C00
//   PURPLE     B026FF
//   CYAN       00E5FF
//   MAGENTA    FF2A6D
//   GRID       1E243A

const pptxgen = require("pptxgenjs");

const C = {
  BG_DEEP:  "0A0F1F",
  BG_PANEL: "141828",
  BG_CODE:  "0D1224",
  TEXT:     "F0F2FA",
  MUTED:    "8A8FA3",
  ORANGE:   "FF8C00",
  PURPLE:   "B026FF",
  CYAN:     "00E5FF",
  MAGENTA:  "FF2A6D",
  GRID:     "1E243A",
  CARDLINE: "2A3052",
};

const FONT_MONO = "Consolas";
const FONT_HEAD = "Calibri";
const FONT_BODY = "Calibri";

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE";   // 13.3" x 7.5"
pres.title  = "CustomAgentHarness on Microsoft Agent 365";
pres.author = "admin@example.org";

const W = 13.333;
const H = 7.5;

// ---------- chrome helpers ----------

function chrome(slide, sectionTag, slideNumber, totalSlides) {
  slide.background = { color: C.BG_DEEP };

  // top hairline
  slide.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: 0, w: W, h: 0.04, fill: { color: C.ORANGE }, line: { type: "none" },
  });
  // bottom hairline gradient stand-in: 3 segments
  slide.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: H - 0.05, w: W / 3, h: 0.05, fill: { color: C.ORANGE }, line: { type: "none" },
  });
  slide.addShape(pres.shapes.RECTANGLE, {
    x: W / 3, y: H - 0.05, w: W / 3, h: 0.05, fill: { color: C.PURPLE }, line: { type: "none" },
  });
  slide.addShape(pres.shapes.RECTANGLE, {
    x: (2 * W) / 3, y: H - 0.05, w: W / 3, h: 0.05, fill: { color: C.CYAN }, line: { type: "none" },
  });

  // section tag (top-left)
  slide.addText(`// ${sectionTag}`, {
    x: 0.4, y: 0.1, w: 6, h: 0.3,
    fontFace: FONT_MONO, fontSize: 11, color: C.CYAN, bold: true, margin: 0,
  });

  // brand (top-right)
  slide.addText("CustomAgentHarness  ×  Agent 365", {
    x: W - 5.4, y: 0.1, w: 5, h: 0.3,
    fontFace: FONT_MONO, fontSize: 11, color: C.MUTED, align: "right", margin: 0,
  });

  // page number (bottom-right)
  slide.addText(`${String(slideNumber).padStart(2, "0")} / ${String(totalSlides).padStart(2, "0")}`, {
    x: W - 1.5, y: H - 0.45, w: 1.2, h: 0.3,
    fontFace: FONT_MONO, fontSize: 10, color: C.MUTED, align: "right", margin: 0,
  });

  // workshop date / venue (bottom-left)
  slide.addText("AgenticBank IT Workshop  ·  Customer-Owned Agents on Agent 365", {
    x: 0.4, y: H - 0.45, w: 8, h: 0.3,
    fontFace: FONT_MONO, fontSize: 10, color: C.MUTED, margin: 0,
  });
}

function title(slide, prefix, text) {
  // prefix like "01"
  slide.addText(prefix, {
    x: 0.55, y: 0.55, w: 1.2, h: 0.9,
    fontFace: FONT_MONO, fontSize: 56, color: C.ORANGE, bold: true,
    valign: "middle", margin: 0,
  });
  // vertical separator
  slide.addShape(pres.shapes.RECTANGLE, {
    x: 1.75, y: 0.65, w: 0.025, h: 0.75, fill: { color: C.CYAN }, line: { type: "none" },
  });
  slide.addText(text, {
    x: 1.95, y: 0.55, w: W - 2.3, h: 0.9,
    fontFace: FONT_HEAD, fontSize: 34, color: C.TEXT, bold: true,
    valign: "middle", margin: 0,
  });
}

function panel(slide, x, y, w, h, color = C.CARDLINE) {
  slide.addShape(pres.shapes.RECTANGLE, {
    x, y, w, h, fill: { color: C.BG_PANEL },
    line: { color, width: 0.75 },
  });
}

function accentBar(slide, x, y, h, color) {
  slide.addShape(pres.shapes.RECTANGLE, {
    x, y, w: 0.08, h, fill: { color }, line: { type: "none" },
  });
}

function codeBlock(slide, x, y, w, h, lines, accent = C.CYAN) {
  slide.addShape(pres.shapes.RECTANGLE, {
    x, y, w, h, fill: { color: C.BG_CODE }, line: { color: accent, width: 0.5 },
  });
  // window dots
  slide.addShape(pres.shapes.OVAL, { x: x + 0.12, y: y + 0.1, w: 0.12, h: 0.12, fill: { color: C.MAGENTA }, line: { type: "none" } });
  slide.addShape(pres.shapes.OVAL, { x: x + 0.30, y: y + 0.1, w: 0.12, h: 0.12, fill: { color: C.ORANGE  }, line: { type: "none" } });
  slide.addShape(pres.shapes.OVAL, { x: x + 0.48, y: y + 0.1, w: 0.12, h: 0.12, fill: { color: "53D169" }, line: { type: "none" } });
  // body
  slide.addText(lines.map((l, i) => ({ text: l, options: { breakLine: i < lines.length - 1 } })), {
    x: x + 0.2, y: y + 0.35, w: w - 0.3, h: h - 0.45,
    fontFace: FONT_MONO, fontSize: 12, color: C.TEXT, valign: "top", margin: 0,
  });
}

function bullets(slide, x, y, w, h, items, opts = {}) {
  const arr = items.map((t, i) => ({
    text: t,
    options: { bullet: true, breakLine: i < items.length - 1, paraSpaceAfter: 6 },
  }));
  slide.addText(arr, {
    x, y, w, h,
    fontFace: FONT_BODY, fontSize: opts.fontSize || 16, color: opts.color || C.TEXT,
    valign: "top", margin: 0,
  });
}

function tagPill(slide, x, y, text, color) {
  const w = 0.18 + (text.length * 0.085);
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x, y, w, h: 0.32,
    fill: { color: C.BG_PANEL },
    line: { color, width: 1 },
    rectRadius: 0.08,
  });
  slide.addText(text, {
    x, y, w, h: 0.32,
    fontFace: FONT_MONO, fontSize: 10, color, bold: true,
    align: "center", valign: "middle", margin: 0,
  });
}

// total slide count is dynamic but we want page numbers; track after build
const TOTAL = 30;
let SLIDE_NO = 0;
function newSlide(sectionTag) {
  SLIDE_NO++;
  const s = pres.addSlide();
  chrome(s, sectionTag, SLIDE_NO, TOTAL);
  return s;
}

// ============================================================
// SLIDE 01 — TITLE
// ============================================================
{
  const s = pres.addSlide();
  s.background = { color: C.BG_DEEP };
  SLIDE_NO++;

  // big orange & purple grid behind title (decorative bars)
  for (let i = 0; i < 8; i++) {
    s.addShape(pres.shapes.RECTANGLE, {
      x: 9.5, y: 1.0 + i * 0.6, w: 3.4, h: 0.04,
      fill: { color: i % 2 === 0 ? C.ORANGE : C.PURPLE },
      line: { type: "none" },
    });
  }
  // cyan accent vertical
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.6, y: 2.0, w: 0.05, h: 3.5,
    fill: { color: C.CYAN }, line: { type: "none" },
  });

  s.addText("// build-and-govern", {
    x: 0.9, y: 1.8, w: 8, h: 0.4,
    fontFace: FONT_MONO, fontSize: 14, color: C.CYAN, bold: true, margin: 0,
  });
  s.addText("Customer-Owned Agents on", {
    x: 0.9, y: 2.3, w: 11.5, h: 0.9,
    fontFace: FONT_HEAD, fontSize: 44, color: C.TEXT, bold: true, margin: 0,
  });
  s.addText("Microsoft Agent 365", {
    x: 0.9, y: 3.2, w: 11.5, h: 1.0,
    fontFace: FONT_HEAD, fontSize: 60, color: C.ORANGE, bold: true, margin: 0,
  });
  s.addText("A 3-hour workshop for IT architects, developers, systems & security", {
    x: 0.9, y: 4.4, w: 11.5, h: 0.5,
    fontFace: FONT_BODY, fontSize: 18, color: C.MUTED, italic: true, margin: 0,
  });

  // bottom info row
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.6, y: 5.6, w: 12, h: 0.04, fill: { color: C.PURPLE }, line: { type: "none" },
  });
  s.addText("AgenticBank IT", { x: 0.9, y: 5.75, w: 4, h: 0.4, fontFace: FONT_MONO, fontSize: 14, color: C.TEXT, bold: true, margin: 0 });
  s.addText("admin@example.org", { x: 5,   y: 5.75, w: 4, h: 0.4, fontFace: FONT_MONO, fontSize: 14, color: C.CYAN, margin: 0 });
  s.addText("tenant: example.org", { x: 9, y: 5.75, w: 4, h: 0.4, fontFace: FONT_MONO, fontSize: 14, color: C.MUTED, align: "right", margin: 0 });

  s.addText("$ harness demo --workshop", {
    x: 0.9, y: 6.4, w: 11, h: 0.5,
    fontFace: FONT_MONO, fontSize: 16, color: C.MAGENTA, bold: true, margin: 0,
  });

  // hairlines
  s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0,       w: W,    h: 0.06, fill: { color: C.ORANGE }, line: { type: "none" } });
  s.addShape(pres.shapes.RECTANGLE, { x: 0, y: H-0.06,  w: W/3,  h: 0.06, fill: { color: C.ORANGE }, line: { type: "none" } });
  s.addShape(pres.shapes.RECTANGLE, { x: W/3, y: H-0.06, w: W/3, h: 0.06, fill: { color: C.PURPLE }, line: { type: "none" } });
  s.addShape(pres.shapes.RECTANGLE, { x: 2*W/3, y: H-0.06, w: W/3, h: 0.06, fill: { color: C.CYAN  }, line: { type: "none" } });
}

// ============================================================
// SLIDE 02 — AGENDA
// ============================================================
{
  const s = newSlide("agenda.md");
  title(s, "02", "Three hours, one story arc");

  const rows = [
    ["00:00", "Welcome · Microsoft AI landscape",              C.ORANGE],
    ["00:15", "Agent 365 concepts (Blueprint, Identity, Purview, Defender, Tools)", C.ORANGE],
    ["00:35", "Where YourCustomAgentHarness fits",             C.PURPLE],
    ["00:50", "Demo intro — what you'll see",                  C.PURPLE],
    ["01:00", "TUI demo: harness up → blueprint → SDK boot",   C.CYAN],
    ["01:30", "BREAK (5 min)",                                 C.MUTED],
    ["01:35", "Admin portal · Entra Agent ID · Purview block", C.CYAN],
    ["02:05", "End-user UI: OBO (Mail/Cal) + KB grounding",    C.CYAN],
    ["02:40", "Create-new-agent wizard",                       C.PURPLE],
    ["02:50", "Governance Q&A · leave-behind",                 C.ORANGE],
  ];

  let y = 1.6;
  for (const [time, label, c] of rows) {
    panel(s, 0.6, y, 12.2, 0.45);
    accentBar(s, 0.6, y, 0.45, c);
    s.addText(time, { x: 0.85, y, w: 1.2, h: 0.45, fontFace: FONT_MONO, fontSize: 14, color: c, bold: true, valign: "middle", margin: 0 });
    s.addText(label, { x: 2.2, y, w: 10.4, h: 0.45, fontFace: FONT_BODY, fontSize: 15, color: C.TEXT, valign: "middle", margin: 0 });
    y += 0.55;
  }
}

// ============================================================
// SLIDE 03 — THE OPPORTUNITY
// ============================================================
{
  const s = newSlide("opportunity");
  title(s, "03", "Agents are arriving — your job is to govern them");

  // 3 stat callouts
  const stats = [
    { n: "73%",  l: "of enterprises piloting custom agents in 2025", c: C.ORANGE },
    { n: "1 of 4", l: "agent deployments lacks identity governance", c: C.MAGENTA },
    { n: "Zero", l: "tolerance for sensitive data leaks via prompts", c: C.CYAN },
  ];
  let x = 0.6;
  for (const st of stats) {
    panel(s, x, 1.7, 4.0, 2.1);
    accentBar(s, x, 1.7, 2.1, st.c);
    s.addText(st.n, { x: x + 0.25, y: 1.85, w: 3.6, h: 0.95, fontFace: FONT_HEAD, fontSize: 50, color: st.c, bold: true, margin: 0 });
    s.addText(st.l, { x: x + 0.25, y: 2.85, w: 3.6, h: 0.85, fontFace: FONT_BODY, fontSize: 14, color: C.TEXT, margin: 0 });
    x += 4.15;
  }

  // big takeaway
  panel(s, 0.6, 4.2, 12.2, 2.6, C.ORANGE);
  s.addText("The question is no longer 'should we let teams build agents?'", {
    x: 0.9, y: 4.45, w: 11.6, h: 0.5,
    fontFace: FONT_BODY, fontSize: 18, color: C.MUTED, italic: true, margin: 0,
  });
  s.addText("It's: every agent gets an identity, every prompt gets classified,", {
    x: 0.9, y: 4.95, w: 11.6, h: 0.6,
    fontFace: FONT_HEAD, fontSize: 24, color: C.TEXT, bold: true, margin: 0,
  });
  s.addText("every action shows up in your SOC — without rebuilding your harness.", {
    x: 0.9, y: 5.55, w: 11.6, h: 0.6,
    fontFace: FONT_HEAD, fontSize: 24, color: C.ORANGE, bold: true, margin: 0,
  });
  s.addText("That's what Microsoft Agent 365 delivers.", {
    x: 0.9, y: 6.2, w: 11.6, h: 0.5,
    fontFace: FONT_MONO, fontSize: 16, color: C.CYAN, bold: true, margin: 0,
  });
}

// ============================================================
// SLIDE 04 — MICROSOFT AI LANDSCAPE
// ============================================================
{
  const s = newSlide("landscape");
  title(s, "04", "Where Agent 365 sits in the Microsoft AI stack");

  const layers = [
    { name: "Compute & Models",       items: "Azure AI Foundry · Azure OpenAI · Hosted + BYO models",                   c: C.CYAN },
    { name: "Agents & Orchestration", items: "Microsoft 365 Copilot · Copilot Studio · Custom code (yours)",            c: C.PURPLE },
    { name: "Agent 365",              items: "Identity (Entra) · Observability (OTel) · Tools (Work IQ) · Governance",  c: C.ORANGE },
    { name: "Data & Knowledge",       items: "Microsoft Graph · SharePoint · Dataverse · your KB / MCP",                c: C.CYAN },
    { name: "Security & Compliance",  items: "Purview (DLP, audit) · Defender for Cloud Apps · Entra Conditional Access", c: C.MAGENTA },
  ];

  let y = 1.7;
  for (const L of layers) {
    panel(s, 0.6, y, 12.2, 0.85);
    accentBar(s, 0.6, y, 0.85, L.c);
    s.addText(L.name, { x: 0.9, y, w: 3.8, h: 0.85, fontFace: FONT_HEAD, fontSize: 18, color: L.c, bold: true, valign: "middle", margin: 0 });
    s.addText(L.items, { x: 4.8, y, w: 7.8, h: 0.85, fontFace: FONT_BODY, fontSize: 14, color: C.TEXT, valign: "middle", margin: 0 });
    y += 1.0;
  }
}

// ============================================================
// SLIDE 05 — WHAT IS AGENT 365 (1-PAGER)
// ============================================================
{
  const s = newSlide("definition");
  title(s, "05", "Microsoft Agent 365 in one sentence");

  panel(s, 0.6, 1.8, 12.2, 1.4, C.CYAN);
  s.addText(
    'A control plane that gives every agent an identity, a registration, a governance perimeter, and a tool surface — without forcing you to change your runtime.',
    { x: 0.9, y: 1.9, w: 11.6, h: 1.2, fontFace: FONT_HEAD, fontSize: 22, color: C.TEXT, italic: true, valign: "middle", margin: 0 },
  );

  // 4 pillars
  const pillars = [
    { name: "Identity",      sub: "Entra Agent Blueprint → Agent ID", c: C.ORANGE },
    { name: "Observability", sub: "OpenTelemetry → Agent 365 export", c: C.PURPLE },
    { name: "Tools",         sub: "Work IQ + MCP + Graph",            c: C.CYAN   },
    { name: "Governance",    sub: "Purview · Defender · Admin Center", c: C.MAGENTA },
  ];
  let x = 0.6;
  for (const p of pillars) {
    panel(s, x, 3.5, 2.95, 3.0);
    accentBar(s, x, 3.5, 3.0, p.c);
    s.addText(p.name, { x: x + 0.25, y: 3.75, w: 2.65, h: 0.7, fontFace: FONT_HEAD, fontSize: 24, color: p.c, bold: true, margin: 0 });
    s.addText(p.sub,  { x: x + 0.25, y: 4.55, w: 2.65, h: 1.8, fontFace: FONT_BODY, fontSize: 14, color: C.TEXT, margin: 0 });
    x += 3.1;
  }
}

// ============================================================
// SLIDE 06 — ENTRA AGENT BLUEPRINT
// ============================================================
{
  const s = newSlide("entra.blueprint");
  title(s, "06", "Entra Agent Blueprint — the identity manifest");

  // left: what
  panel(s, 0.6, 1.7, 6.0, 5.2);
  accentBar(s, 0.6, 1.7, 5.2, C.ORANGE);
  s.addText("What it is", { x: 0.85, y: 1.85, w: 5.6, h: 0.45, fontFace: FONT_HEAD, fontSize: 22, color: C.ORANGE, bold: true, margin: 0 });
  bullets(s, 0.85, 2.4, 5.6, 4.2, [
    "Declarative YAML/JSON that defines an agent's persona, owner, sponsor",
    "Lists the Graph delegated + application permissions it may ever request",
    "Declares the Purview sensitive-information types it must reason about",
    "Captures the model deployment & endpoint binding (Foundry)",
    "Becomes the source-of-truth — promoted through dev→test→prod via change control",
    "Registers with Entra; mints the Agent ID; lights up Admin Center",
  ], { fontSize: 14 });

  // right: yaml snippet
  panel(s, 6.8, 1.7, 6.0, 5.2);
  accentBar(s, 6.8, 1.7, 5.2, C.CYAN);
  s.addText("Sample (excerpt)", { x: 7.05, y: 1.85, w: 5.6, h: 0.45, fontFace: FONT_HEAD, fontSize: 18, color: C.CYAN, bold: true, margin: 0 });
  codeBlock(s, 7.05, 2.4, 5.55, 4.3, [
    "apiVersion: customagentharness/v1",
    "kind: AgentBlueprint",
    "metadata:",
    "  name: ForgedAgentOne",
    "  owner: admin@example.org",
    "  sponsor: admin@example.org",
    "spec:",
    "  identity:",
    "    authMode: delegated",
    "    delegatedPermissions:",
    "      - Mail.Read",
    "      - Calendars.Read",
    "  model:",
    "    provider: AzureOpenAI",
    "    deploymentName: gpt-4.1",
    "  governance:",
    "    purview:",
    "      sensitiveInformationTypes:",
    "        - USSocialSecurityNumber",
    "        - IBAN",
  ]);
}

// ============================================================
// SLIDE 07 — ENTRA AGENT ID
// ============================================================
{
  const s = newSlide("entra.agent-id");
  title(s, "07", "Entra Agent ID — first-class identity for agents");

  // 2-column: ServicePrincipal vs Agent ID
  panel(s, 0.6, 1.7, 6.0, 5.2);
  accentBar(s, 0.6, 1.7, 5.2, C.MUTED);
  s.addText("Yesterday: Service Principal", { x: 0.85, y: 1.85, w: 5.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 20, color: C.MUTED, bold: true, margin: 0 });
  bullets(s, 0.85, 2.5, 5.6, 4.2, [
    "Designed for apps & daemons, not autonomous agents",
    "No notion of sponsor / owner accountability",
    "No blueprint linkage → drift between code and identity",
    "Hard to answer: 'who let this agent into Outlook?'",
    "Logs land in sign-in logs as a generic workload identity",
  ], { fontSize: 14 });

  panel(s, 6.8, 1.7, 6.0, 5.2);
  accentBar(s, 6.8, 1.7, 5.2, C.ORANGE);
  s.addText("Today: Entra Agent ID", { x: 7.05, y: 1.85, w: 5.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 20, color: C.ORANGE, bold: true, margin: 0 });
  bullets(s, 7.05, 2.5, 5.6, 4.2, [
    "Purpose-built identity object minted from a Blueprint",
    "Carries owner + sponsor as first-class attributes",
    "Shows up in the M365 admin center agent catalog",
    "Receives OBO tokens scoped exactly to its declared permissions",
    "Audit + sign-in events tagged with AgentId for SOC pivots",
  ], { fontSize: 14 });
}

// ============================================================
// SLIDE 08 — TWO IDENTITY PATTERNS
// ============================================================
{
  const s = newSlide("patterns");
  title(s, "08", "Two identity patterns you'll use");

  // OBO card
  panel(s, 0.6, 1.7, 6.0, 5.2);
  accentBar(s, 0.6, 1.7, 5.2, C.PURPLE);
  s.addText("A · Delegated (OBO)", { x: 0.85, y: 1.85, w: 5.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 22, color: C.PURPLE, bold: true, margin: 0 });
  s.addText("Agent acts as the user", { x: 0.85, y: 2.4, w: 5.6, h: 0.35, fontFace: FONT_BODY, fontSize: 13, color: C.MUTED, italic: true, margin: 0 });
  bullets(s, 0.85, 2.85, 5.6, 2.4, [
    "User signs in via MSAL.js → SPA gets Graph token",
    "SPA passes user assertion to agent",
    "Agent exchanges assertion for OBO token (scope: Mail.Read)",
    "All reads honor the user's existing ACLs",
  ], { fontSize: 13 });
  s.addText("Demo: ForgedAgentOne reads your unread mail", {
    x: 0.85, y: 5.7, w: 5.6, h: 0.4, fontFace: FONT_MONO, fontSize: 12, color: C.CYAN, bold: true, margin: 0,
  });
  tagPill(s, 0.85, 6.2, "delegated", C.PURPLE);
  tagPill(s, 2.1,  6.2, "Mail.Read", C.CYAN);
  tagPill(s, 3.55, 6.2, "Calendars.Read", C.CYAN);

  // App-perm card
  panel(s, 6.8, 1.7, 6.0, 5.2);
  accentBar(s, 6.8, 1.7, 5.2, C.ORANGE);
  s.addText("B · Application Permission", { x: 7.05, y: 1.85, w: 5.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 22, color: C.ORANGE, bold: true, margin: 0 });
  s.addText("Agent acts on its own behalf", { x: 7.05, y: 2.4, w: 5.6, h: 0.35, fontFace: FONT_BODY, fontSize: 13, color: C.MUTED, italic: true, margin: 0 });
  bullets(s, 7.05, 2.85, 5.6, 2.4, [
    "Agent owns its own scope — no user context required",
    "Reads from its own Knowledge source (MCP server, RAG store, DB)",
    "Useful for shared/internal Q&A workloads",
    "Permissions explicitly bounded by the Blueprint",
  ], { fontSize: 13 });
  s.addText("Demo: ForgedScholarTwo answers policy questions from bank KB", {
    x: 7.05, y: 5.7, w: 5.6, h: 0.4, fontFace: FONT_MONO, fontSize: 12, color: C.CYAN, bold: true, margin: 0,
  });
  tagPill(s, 7.05, 6.2, "application", C.ORANGE);
  tagPill(s, 8.45, 6.2, "MCP:kb", C.CYAN);
  tagPill(s, 9.7,  6.2, "no Graph", C.MUTED);
}

// ============================================================
// SLIDE 09 — PURVIEW INTEGRATION
// ============================================================
{
  const s = newSlide("purview");
  title(s, "09", "Purview — bidirectional content protection");

  // diagram
  panel(s, 0.6, 1.7, 12.2, 3.2);
  s.addText("Prompt protection runs in BOTH directions", {
    x: 0.85, y: 1.85, w: 11.5, h: 0.4, fontFace: FONT_HEAD, fontSize: 18, color: C.CYAN, bold: true, margin: 0,
  });

  // user box
  panel(s, 0.9, 2.5, 2.3, 2.0, C.PURPLE);
  s.addText("USER", { x: 0.9, y: 2.55, w: 2.3, h: 0.4, fontFace: FONT_MONO, fontSize: 14, color: C.PURPLE, bold: true, align: "center", margin: 0 });
  s.addText("end-user types a prompt", { x: 0.95, y: 3.05, w: 2.2, h: 1.3, fontFace: FONT_BODY, fontSize: 12, color: C.TEXT, align: "center", margin: 0 });

  // arrow + classify
  panel(s, 3.8, 2.5, 2.5, 2.0, C.CYAN);
  s.addText("CLASSIFY ▶", { x: 3.8, y: 2.55, w: 2.5, h: 0.4, fontFace: FONT_MONO, fontSize: 13, color: C.CYAN, bold: true, align: "center", margin: 0 });
  s.addText("Purview SDK · SIT match → ALLOW / BLOCK", { x: 3.9, y: 3.05, w: 2.3, h: 1.3, fontFace: FONT_BODY, fontSize: 12, color: C.TEXT, align: "center", margin: 0 });

  // agent
  panel(s, 6.9, 2.5, 2.3, 2.0, C.ORANGE);
  s.addText("AGENT", { x: 6.9, y: 2.55, w: 2.3, h: 0.4, fontFace: FONT_MONO, fontSize: 14, color: C.ORANGE, bold: true, align: "center", margin: 0 });
  s.addText("reasons + drafts a response", { x: 6.95, y: 3.05, w: 2.2, h: 1.3, fontFace: FONT_BODY, fontSize: 12, color: C.TEXT, align: "center", margin: 0 });

  // classify back
  panel(s, 9.8, 2.5, 2.5, 2.0, C.CYAN);
  s.addText("◀ CLASSIFY", { x: 9.8, y: 2.55, w: 2.5, h: 0.4, fontFace: FONT_MONO, fontSize: 13, color: C.CYAN, bold: true, align: "center", margin: 0 });
  s.addText("response screened before it leaves", { x: 9.9, y: 3.05, w: 2.3, h: 1.3, fontFace: FONT_BODY, fontSize: 12, color: C.TEXT, align: "center", margin: 0 });

  // arrows (decorative)
  s.addShape(pres.shapes.LINE, { x: 3.2, y: 3.5, w: 0.6, h: 0, line: { color: C.CYAN, width: 2, endArrowType: "triangle" } });
  s.addShape(pres.shapes.LINE, { x: 6.3, y: 3.5, w: 0.6, h: 0, line: { color: C.CYAN, width: 2, endArrowType: "triangle" } });
  s.addShape(pres.shapes.LINE, { x: 9.2, y: 3.5, w: 0.6, h: 0, line: { color: C.CYAN, width: 2, endArrowType: "triangle" } });

  // what gets blocked
  panel(s, 0.6, 5.1, 12.2, 1.8, C.MAGENTA);
  s.addText("In tonight's demo, the harness blocks:", { x: 0.85, y: 5.2, w: 11.5, h: 0.4, fontFace: FONT_HEAD, fontSize: 16, color: C.MAGENTA, bold: true, margin: 0 });
  s.addText([
    { text: "SSN  ",      options: { color: C.CYAN, bold: true } },
    { text: "IBAN  ",     options: { color: C.CYAN, bold: true } },
    { text: "SWIFT  ",    options: { color: C.CYAN, bold: true } },
    { text: "Credit Card  ", options: { color: C.CYAN, bold: true } },
    { text: "Customer Tax ID  ", options: { color: C.CYAN, bold: true } },
    { text: "Internal Account Number  ", options: { color: C.CYAN, bold: true } },
    { text: "Email", options: { color: C.CYAN, bold: true } },
  ], {
    x: 0.85, y: 5.65, w: 11.5, h: 0.5, fontFace: FONT_MONO, fontSize: 14, margin: 0,
  });
  s.addText("Auto mode → Purview SDK if scopes wired; Fallback mode → deterministic SIT regex (today's demo).", {
    x: 0.85, y: 6.25, w: 11.5, h: 0.5, fontFace: FONT_BODY, fontSize: 13, color: C.TEXT, margin: 0,
  });
}

// ============================================================
// SLIDE 10 — DEFENDER FOR CLOUD APPS
// ============================================================
{
  const s = newSlide("defender");
  title(s, "10", "Defender for Cloud Apps — the SOC view");

  panel(s, 0.6, 1.7, 6.0, 5.2);
  accentBar(s, 0.6, 1.7, 5.2, C.MAGENTA);
  s.addText("Signal flow", { x: 0.85, y: 1.85, w: 5.6, h: 0.45, fontFace: FONT_HEAD, fontSize: 20, color: C.MAGENTA, bold: true, margin: 0 });
  bullets(s, 0.85, 2.4, 5.6, 4.2, [
    "Agent activity hits Entra sign-in & audit logs",
    "MDA ingests as a connected app — agent shows up alongside SaaS",
    "Anomaly policies fire on unusual hours, locations, volumes",
    "Policy: 'agent attempted action outside blueprint permission set' → high severity",
    "Pivot to KQL in Sentinel using the Agent ID",
  ], { fontSize: 14 });

  panel(s, 6.8, 1.7, 6.0, 5.2);
  accentBar(s, 6.8, 1.7, 5.2, C.ORANGE);
  s.addText("Recommended policies", { x: 7.05, y: 1.85, w: 5.6, h: 0.45, fontFace: FONT_HEAD, fontSize: 20, color: C.ORANGE, bold: true, margin: 0 });
  bullets(s, 7.05, 2.4, 5.6, 4.2, [
    "Alert on first-time Graph endpoint called by an agent",
    "Alert on token exchange failures > 5/min for one agent",
    "Throttle outbound when prompt-block rate > 10% (likely abuse)",
    "Auto-disable agent on 'Blueprint drift' (live perms ≠ manifest)",
    "Daily digest: top 10 agents by Graph call volume + cost",
  ], { fontSize: 14 });
}

// ============================================================
// SLIDE 11 — ADMIN CENTER
// ============================================================
{
  const s = newSlide("admin.microsoft.com");
  title(s, "11", "Microsoft 365 Admin Center — agent catalog");

  panel(s, 0.6, 1.7, 12.2, 2.5);
  s.addText("Every registered agent appears in admin.microsoft.com → Agents", {
    x: 0.85, y: 1.85, w: 11.5, h: 0.5, fontFace: FONT_HEAD, fontSize: 18, color: C.CYAN, bold: true, margin: 0,
  });
  bullets(s, 0.85, 2.45, 11.5, 1.7, [
    "Sponsor sees lifecycle status (Active · Suspended · Pending review)",
    "IT admin can suspend an agent across the tenant with one click",
    "License consumption surfaced per agent, per user",
    "Same UX teams already know from Teams / Copilot Studio app management",
  ], { fontSize: 14 });

  // 3 portal links you'll actually visit
  const links = [
    { label: "admin.microsoft.com", path: "→ Settings → Integrated apps → Agents", c: C.ORANGE },
    { label: "entra.microsoft.com", path: "→ Identity → Applications → Agent IDs", c: C.PURPLE },
    { label: "purview.microsoft.com", path: "→ Data Security → DLP for AI Apps", c: C.CYAN },
  ];
  let x = 0.6;
  for (const lk of links) {
    panel(s, x, 4.5, 4.0, 2.3);
    accentBar(s, x, 4.5, 2.3, lk.c);
    s.addText(lk.label, { x: x + 0.25, y: 4.65, w: 3.6, h: 0.55, fontFace: FONT_MONO, fontSize: 16, color: lk.c, bold: true, margin: 0 });
    s.addText(lk.path,  { x: x + 0.25, y: 5.3,  w: 3.6, h: 1.4,  fontFace: FONT_BODY, fontSize: 13, color: C.TEXT, margin: 0 });
    x += 4.15;
  }
}

// ============================================================
// SLIDE 12 — WHERE YOUR HARNESS FITS
// ============================================================
{
  const s = newSlide("integration");
  title(s, "12", "Where YourCustomAgentHarness fits");

  panel(s, 0.6, 1.7, 12.2, 1.5, C.ORANGE);
  s.addText("Your harness stays. You wire in Agent 365 SDK at four specific seams.", {
    x: 0.85, y: 1.95, w: 11.7, h: 0.9, fontFace: FONT_HEAD, fontSize: 22, color: C.TEXT, bold: true, valign: "middle", margin: 0,
  });

  const seams = [
    { n: "01", t: "Identity",      d: "Replace ad-hoc creds with Entra Agent ID minted from Blueprint",  c: C.ORANGE },
    { n: "02", t: "Registration",  d: "Push appManifest to Microsoft 365 admin center on agent boot",     c: C.PURPLE },
    { n: "03", t: "Content protection", d: "Wrap prompt I/O with Purview SDK (or harness fallback)",      c: C.CYAN },
    { n: "04", t: "Observability", d: "OpenTelemetry exporter → Agent 365 (in addition to your APM)",     c: C.MAGENTA },
  ];
  let y = 3.5;
  for (const seam of seams) {
    panel(s, 0.6, y, 12.2, 0.75);
    accentBar(s, 0.6, y, 0.75, seam.c);
    s.addText(seam.n, { x: 0.9, y, w: 0.8, h: 0.75, fontFace: FONT_MONO, fontSize: 22, color: seam.c, bold: true, valign: "middle", margin: 0 });
    s.addText(seam.t, { x: 1.8, y, w: 2.8, h: 0.75, fontFace: FONT_HEAD, fontSize: 17, color: C.TEXT, bold: true, valign: "middle", margin: 0 });
    s.addText(seam.d, { x: 4.7, y, w: 8.0, h: 0.75, fontFace: FONT_BODY, fontSize: 14, color: C.MUTED, valign: "middle", margin: 0 });
    y += 0.85;
  }
}

// ============================================================
// SLIDE 13 — REFERENCE ARCHITECTURE
// ============================================================
{
  const s = newSlide("architecture");
  title(s, "13", "Reference architecture — tonight's running system");

  // 4 zones laid out horizontally
  // Zone 1: End user
  panel(s, 0.4, 1.7, 2.6, 5.0, C.PURPLE);
  s.addText("END USER", { x: 0.4, y: 1.8, w: 2.6, h: 0.4, fontFace: FONT_MONO, fontSize: 12, color: C.PURPLE, bold: true, align: "center", margin: 0 });
  s.addText("Browser\nMSAL.js SPA", { x: 0.5, y: 2.3, w: 2.4, h: 0.9, fontFace: FONT_BODY, fontSize: 14, color: C.TEXT, align: "center", margin: 0 });
  s.addText("→ /hub UI", { x: 0.5, y: 3.3, w: 2.4, h: 0.4, fontFace: FONT_MONO, fontSize: 12, color: C.CYAN, align: "center", margin: 0 });
  s.addText("→ /admin UI", { x: 0.5, y: 3.7, w: 2.4, h: 0.4, fontFace: FONT_MONO, fontSize: 12, color: C.CYAN, align: "center", margin: 0 });
  s.addText("(localhost:4001)", { x: 0.5, y: 6.1, w: 2.4, h: 0.4, fontFace: FONT_MONO, fontSize: 10, color: C.MUTED, align: "center", margin: 0 });

  // Zone 2: Harness
  panel(s, 3.2, 1.7, 4.0, 5.0, C.ORANGE);
  s.addText("CUSTOMAGENTHARNESS", { x: 3.2, y: 1.8, w: 4.0, h: 0.4, fontFace: FONT_MONO, fontSize: 12, color: C.ORANGE, bold: true, align: "center", margin: 0 });
  // sub-boxes
  panel(s, 3.35, 2.35, 3.7, 0.7);
  s.addText("harness.api  :4000", { x: 3.35, y: 2.35, w: 3.7, h: 0.7, fontFace: FONT_MONO, fontSize: 13, color: C.TEXT, align: "center", valign: "middle", margin: 0 });
  panel(s, 3.35, 3.15, 3.7, 0.7);
  s.addText("ForgedAgentOne :3979", { x: 3.35, y: 3.15, w: 3.7, h: 0.7, fontFace: FONT_MONO, fontSize: 13, color: C.PURPLE, align: "center", valign: "middle", margin: 0 });
  panel(s, 3.35, 3.95, 3.7, 0.7);
  s.addText("ForgedScholarTwo :3980", { x: 3.35, y: 3.95, w: 3.7, h: 0.7, fontFace: FONT_MONO, fontSize: 13, color: C.ORANGE, align: "center", valign: "middle", margin: 0 });
  panel(s, 3.35, 4.75, 3.7, 0.7);
  s.addText("kb-mcp :3981", { x: 3.35, y: 4.75, w: 3.7, h: 0.7, fontFace: FONT_MONO, fontSize: 13, color: C.CYAN, align: "center", valign: "middle", margin: 0 });
  panel(s, 3.35, 5.55, 3.7, 0.7);
  s.addText("Agent 365 SDK", { x: 3.35, y: 5.55, w: 3.7, h: 0.7, fontFace: FONT_MONO, fontSize: 12, color: C.MAGENTA, align: "center", valign: "middle", margin: 0 });
  s.addText("(local machine)", { x: 3.35, y: 6.4, w: 3.7, h: 0.3, fontFace: FONT_MONO, fontSize: 10, color: C.MUTED, align: "center", margin: 0 });

  // Zone 3: Azure
  panel(s, 7.4, 1.7, 2.8, 5.0, C.CYAN);
  s.addText("AZURE", { x: 7.4, y: 1.8, w: 2.8, h: 0.4, fontFace: FONT_MONO, fontSize: 12, color: C.CYAN, bold: true, align: "center", margin: 0 });
  panel(s, 7.55, 2.35, 2.5, 0.7);
  s.addText("Foundry\ngpt-4.1", { x: 7.55, y: 2.35, w: 2.5, h: 0.7, fontFace: FONT_MONO, fontSize: 12, color: C.TEXT, align: "center", valign: "middle", margin: 0 });
  panel(s, 7.55, 3.15, 2.5, 0.7);
  s.addText("text-embed-3-sm", { x: 7.55, y: 3.15, w: 2.5, h: 0.7, fontFace: FONT_MONO, fontSize: 12, color: C.TEXT, align: "center", valign: "middle", margin: 0 });
  panel(s, 7.55, 3.95, 2.5, 0.7);
  s.addText("Entra ID\nAgent Blueprint", { x: 7.55, y: 3.95, w: 2.5, h: 0.7, fontFace: FONT_MONO, fontSize: 11, color: C.PURPLE, align: "center", valign: "middle", margin: 0 });
  panel(s, 7.55, 4.75, 2.5, 0.7);
  s.addText("Microsoft Graph", { x: 7.55, y: 4.75, w: 2.5, h: 0.7, fontFace: FONT_MONO, fontSize: 11, color: C.TEXT, align: "center", valign: "middle", margin: 0 });
  panel(s, 7.55, 5.55, 2.5, 0.7);
  s.addText("Purview SDK", { x: 7.55, y: 5.55, w: 2.5, h: 0.7, fontFace: FONT_MONO, fontSize: 12, color: C.MAGENTA, align: "center", valign: "middle", margin: 0 });

  // Zone 4: Portals
  panel(s, 10.4, 1.7, 2.7, 5.0, C.MAGENTA);
  s.addText("PORTALS", { x: 10.4, y: 1.8, w: 2.7, h: 0.4, fontFace: FONT_MONO, fontSize: 12, color: C.MAGENTA, bold: true, align: "center", margin: 0 });
  const portals = [
    "admin.microsoft.com",
    "entra.microsoft.com",
    "purview.microsoft.com",
    "ai.azure.com",
    "portal.azure.com",
  ];
  let py = 2.35;
  for (const p of portals) {
    panel(s, 10.55, py, 2.4, 0.7);
    s.addText(p, { x: 10.55, y: py, w: 2.4, h: 0.7, fontFace: FONT_MONO, fontSize: 11, color: C.TEXT, align: "center", valign: "middle", margin: 0 });
    py += 0.8;
  }
}

// ============================================================
// SLIDE 14 — DEMO INTRO
// ============================================================
{
  const s = newSlide("demo");
  title(s, "14", "Demo — 5 chapters, 90 minutes");

  const chapters = [
    { n: "1", t: "Boot the harness",      d: "harness up → blueprint validation → process tree comes alive", c: C.ORANGE },
    { n: "2", t: "Mint Agent identity",   d: "Register blueprint → see Agent ID in Entra → pin in admin center", c: C.PURPLE },
    { n: "3", t: "Bind model + SDK",      d: "Foundry deployment via DefaultAzureCredential — no API keys", c: C.CYAN },
    { n: "4", t: "Govern the prompt",     d: "Purview SDK blocks SSN, IBAN, internal account # — both directions", c: C.MAGENTA },
    { n: "5", t: "Two agents, two patterns", d: "OBO (Mail/Cal) + Application + KB (MCP) — live in /hub", c: C.ORANGE },
  ];
  let y = 1.7;
  for (const c of chapters) {
    panel(s, 0.6, y, 12.2, 0.95);
    accentBar(s, 0.6, y, 0.95, c.c);
    s.addText(c.n, { x: 0.85, y, w: 0.8, h: 0.95, fontFace: FONT_MONO, fontSize: 36, color: c.c, bold: true, valign: "middle", margin: 0 });
    s.addText(c.t, { x: 1.75, y, w: 3.5, h: 0.95, fontFace: FONT_HEAD, fontSize: 20, color: C.TEXT, bold: true, valign: "middle", margin: 0 });
    s.addText(c.d, { x: 5.35, y, w: 7.3, h: 0.95, fontFace: FONT_BODY, fontSize: 14, color: C.MUTED, valign: "middle", margin: 0 });
    y += 1.05;
  }
}

// ============================================================
// SLIDE 15 — CHAPTER 1 PREVIEW (TUI)
// ============================================================
{
  const s = newSlide("ch.1 / boot");
  title(s, "15", "Chapter 1 — boot the harness");

  panel(s, 0.6, 1.7, 6.0, 5.2);
  accentBar(s, 0.6, 1.7, 5.2, C.ORANGE);
  s.addText("What happens", { x: 0.85, y: 1.85, w: 5.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 20, color: C.ORANGE, bold: true, margin: 0 });
  bullets(s, 0.85, 2.45, 5.6, 4.3, [
    "TUI step machine renders [INTRO] → [COMMAND] → [EXECUTING] → [RESULT] → [VERIFY]",
    "Blueprint YAML schema is validated against the spec",
    "harness up brings up 5 processes: api, web, kb-mcp, ForgedAgentOne, ForgedScholarTwo",
    "Doctor probes — az login, A365 SDK, Foundry reachability, AOAI Entra-auth, Purview policy",
    "What to point out: nothing is hidden — every process logs in /admin Activity Console",
  ], { fontSize: 13 });

  codeBlock(s, 6.8, 1.7, 6.0, 3.0, [
    "PS C:\\customagentharness> harness up",
    "",
    "[ harness ] api          : ready (4000)",
    "[ harness ] web          : ready (4001)",
    "[ harness ] kb-mcp       : ready (3981)",
    "[ harness ] ForgedAgentOne : ready (3979)",
    "[ harness ] ForgedScholarTwo: ready (3980)",
    "",
    "5/5 services healthy   ·   62s",
  ]);

  panel(s, 6.8, 4.9, 6.0, 2.0, C.CYAN);
  s.addText("Portal to open at the end of Ch.1", { x: 7.05, y: 5.05, w: 5.6, h: 0.4, fontFace: FONT_HEAD, fontSize: 14, color: C.CYAN, bold: true, margin: 0 });
  s.addText("→  http://localhost:4001/admin", { x: 7.05, y: 5.5, w: 5.6, h: 0.5, fontFace: FONT_MONO, fontSize: 16, color: C.TEXT, bold: true, margin: 0 });
  s.addText("Live process tree + OTel SSE feed.", { x: 7.05, y: 6.05, w: 5.6, h: 0.5, fontFace: FONT_BODY, fontSize: 13, color: C.MUTED, margin: 0 });
}

// ============================================================
// SLIDE 16 — CHAPTER 2 — BLUEPRINT
// ============================================================
{
  const s = newSlide("ch.2 / blueprint");
  title(s, "16", "Chapter 2 — register the blueprint");

  panel(s, 0.6, 1.7, 6.0, 5.2);
  accentBar(s, 0.6, 1.7, 5.2, C.PURPLE);
  s.addText("What happens", { x: 0.85, y: 1.85, w: 5.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 20, color: C.PURPLE, bold: true, margin: 0 });
  bullets(s, 0.85, 2.45, 5.6, 4.3, [
    "harness blueprint apply forged-agent-one — converts harness YAML → A365 JSON",
    "Calls a365 develop register-blueprint",
    "Entra mints an Agent ID and bonds it to the blueprint object",
    "We pivot to the Entra portal to see the blueprint + Agent ID objects",
    "OwnerId and SponsorId are visible — accountability is now in your IAM tool",
  ], { fontSize: 13 });

  codeBlock(s, 6.8, 1.7, 6.0, 3.4, [
    "PS> harness blueprint apply forged-agent-one",
    "",
    "▶ validating blueprint schema       OK",
    "▶ converting harness → a365 JSON    OK",
    "▶ a365 develop register-blueprint   OK",
    "  ↳ blueprintId  bp_3a8f...",
    "  ↳ agentId      ag_91c2...",
    "  ↳ ownerOid     22222222-2222-...",
    "",
    "Verify → entra.microsoft.com",
  ]);

  panel(s, 6.8, 5.2, 6.0, 1.7, C.CYAN);
  s.addText("entra.microsoft.com → Identity → Applications → Agent Blueprints", {
    x: 7.05, y: 5.35, w: 5.6, h: 0.45, fontFace: FONT_MONO, fontSize: 13, color: C.CYAN, bold: true, margin: 0,
  });
  s.addText("Look for: ForgedAgentOne · owner: admin@example.org · permissions array", {
    x: 7.05, y: 5.85, w: 5.6, h: 0.9, fontFace: FONT_BODY, fontSize: 13, color: C.TEXT, margin: 0,
  });
}

// ============================================================
// SLIDE 17 — CHAPTER 3 — MODEL & SDK
// ============================================================
{
  const s = newSlide("ch.3 / sdk");
  title(s, "17", "Chapter 3 — bind Foundry model + init SDK");

  panel(s, 0.6, 1.7, 6.0, 5.2);
  accentBar(s, 0.6, 1.7, 5.2, C.CYAN);
  s.addText("What happens", { x: 0.85, y: 1.85, w: 5.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 20, color: C.CYAN, bold: true, margin: 0 });
  bullets(s, 0.85, 2.45, 5.6, 4.3, [
    "az cognitiveservices account list  → discover your-foundry-account",
    "Pick deployment gpt-4.1 → write into appsettings",
    "Foundry has disableLocalAuth=true  →  NO API keys used",
    "Agent uses DefaultAzureCredential — same auth your admins already know",
    "Agent 365 SDK boots: OTel trace shows blueprintId + agentId on every span",
  ], { fontSize: 13 });

  codeBlock(s, 6.8, 1.7, 6.0, 3.6, [
    "// ForgedAgentOne/Program.cs",
    "var cred = new DefaultAzureCredential();",
    "builder.Services",
    "  .AddAzureOpenAIChatCompletion(",
    "     deploymentName: \"gpt-4.1\",",
    "     endpoint: foundryEndpoint,",
    "     credentials: cred);",
    "",
    "builder.Services.AddAgent365SDK(opts => {",
    "   opts.BlueprintId = \"bp_3a8f...\";",
    "   opts.AgentId     = \"ag_91c2...\";",
    "});",
  ]);

  panel(s, 6.8, 5.4, 6.0, 1.5, C.ORANGE);
  s.addText("Why this matters to security", { x: 7.05, y: 5.5, w: 5.6, h: 0.4, fontFace: FONT_HEAD, fontSize: 14, color: C.ORANGE, bold: true, margin: 0 });
  s.addText("Conditional Access policies on the agent's identity now gate every model call.", {
    x: 7.05, y: 5.95, w: 5.6, h: 0.85, fontFace: FONT_BODY, fontSize: 13, color: C.TEXT, margin: 0,
  });
}

// ============================================================
// SLIDE 18 — CHAPTER 4 — PURVIEW
// ============================================================
{
  const s = newSlide("ch.4 / purview");
  title(s, "18", "Chapter 4 — Purview blocks the bad prompts");

  // 3 example prompts
  const prompts = [
    { p: "Hi! What's my schedule today?",                  v: "ALLOW", c: C.CYAN,    why: "no SITs matched" },
    { p: "Refund this transaction: 4111-1111-1111-1111",   v: "BLOCK", c: C.MAGENTA, why: "CreditCardNumber" },
    { p: "Send wire to IBAN DE89 3704 0044 0532 0130 00",  v: "BLOCK", c: C.MAGENTA, why: "IBAN, SWIFT context" },
    { p: "Update SSN 123-45-6789 in CRM",                  v: "BLOCK", c: C.MAGENTA, why: "USSocialSecurityNumber" },
    { p: "Anonymized stats from last quarter, please",     v: "ALLOW", c: C.CYAN,    why: "no SITs matched" },
    { p: "Hey agent, what's customer Mei's tax ID?",       v: "BLOCK", c: C.MAGENTA, why: "CustomerTaxId (output)" },
  ];

  let y = 1.7;
  for (const pr of prompts) {
    panel(s, 0.6, y, 12.2, 0.7);
    accentBar(s, 0.6, y, 0.7, pr.c);
    s.addText(pr.v, { x: 0.9, y, w: 1.2, h: 0.7, fontFace: FONT_MONO, fontSize: 14, color: pr.c, bold: true, valign: "middle", margin: 0 });
    s.addText(pr.p, { x: 2.2, y, w: 7.7, h: 0.7, fontFace: FONT_MONO, fontSize: 13, color: C.TEXT, valign: "middle", margin: 0 });
    s.addText(pr.why, { x: 10.0, y, w: 2.8, h: 0.7, fontFace: FONT_BODY, fontSize: 12, color: C.MUTED, italic: true, valign: "middle", margin: 0 });
    y += 0.8;
  }

  panel(s, 0.6, 6.55, 12.2, 0.55, C.CYAN);
  s.addText("Last prompt is blocked on the AGENT → USER direction. Sensitive data never leaves the boundary.", {
    x: 0.85, y: 6.55, w: 11.5, h: 0.55, fontFace: FONT_BODY, fontSize: 13, color: C.CYAN, bold: true, valign: "middle", margin: 0,
  });
}

// ============================================================
// SLIDE 19 — CHAPTER 5 (a) — OBO
// ============================================================
{
  const s = newSlide("ch.5 / obo");
  title(s, "19", "Chapter 5a — OBO · reads YOUR mail");

  panel(s, 0.6, 1.7, 6.0, 5.2);
  accentBar(s, 0.6, 1.7, 5.2, C.PURPLE);
  s.addText("Step-by-step", { x: 0.85, y: 1.85, w: 5.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 20, color: C.PURPLE, bold: true, margin: 0 });
  bullets(s, 0.85, 2.45, 5.6, 4.3, [
    "Open /hub → click 'Sign in with Microsoft'",
    "MSAL.js gets a Graph token for the SIGNED-IN USER",
    "Type: 'summarize my unread emails'",
    "SPA passes the user assertion to ForgedAgentOne",
    "Agent does OBO → calls Graph as YOU (Mail.Read)",
    "Result rendered with provenance: 'sourced from your Outlook'",
  ], { fontSize: 13 });

  codeBlock(s, 6.8, 1.7, 6.0, 5.2, [
    "GET /api/agents/forged-agent-one/chat",
    "  Authorization: Bearer <user-assertion>",
    "",
    "agent.OnExchangeToken(async ctx => {",
    "  var graphToken = await OboTokenAsync(",
    "    ctx.UserAssertion,",
    "    scopes: new[] { \"Mail.Read\" }",
    "  );",
    "  return new GraphClient(graphToken);",
    "});",
    "",
    "▶ Mail.Read   200 OK",
    "▶ user sees: 'You have 3 unread...'",
  ]);
}

// ============================================================
// SLIDE 20 — CHAPTER 5 (b) — APP + KB
// ============================================================
{
  const s = newSlide("ch.5 / app+kb");
  title(s, "20", "Chapter 5b — App-perm · KB-grounded answers");

  panel(s, 0.6, 1.7, 6.0, 5.2);
  accentBar(s, 0.6, 1.7, 5.2, C.ORANGE);
  s.addText("Step-by-step", { x: 0.85, y: 1.85, w: 5.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 20, color: C.ORANGE, bold: true, margin: 0 });
  bullets(s, 0.85, 2.45, 5.6, 4.3, [
    "/hub → pick 'ForgedScholarTwo' from the agent dropdown",
    "Type: 'what's our KYC threshold for high-risk customers?'",
    "Agent does NOT need user delegation — it has its own scope",
    "Agent calls the kb-mcp MCP server (localhost:3981)",
    "KB MCP returns 5 chunks with citations from /kb/agenticbank/*.md",
    "Answer rendered with footnotes — verifiable, grounded",
  ], { fontSize: 13 });

  codeBlock(s, 6.8, 1.7, 6.0, 5.2, [
    "// ForgedScholarTwo prompt path",
    "var hits = await mcpKb.SearchAsync(",
    "  query: userMessage,",
    "  topK: 5);",
    "",
    "var sys = $@\"You are a bank policy",
    "  assistant. Answer ONLY from these",
    "  chunks. Cite [source] inline.",
    "  {string.Join('\\n', hits)}\";",
    "",
    "return await chat.GetResponseAsync(",
    "  sys + userMessage);",
    "",
    "Citations:",
    "  [kyc-policy.md §3.2]",
    "  [risk-tiers.md §1.1]",
  ]);
}

// ============================================================
// SLIDE 21 — NEW AGENT WIZARD
// ============================================================
{
  const s = newSlide("create");
  title(s, "21", "Creating a new agent definition");

  panel(s, 0.6, 1.7, 12.2, 5.2);
  s.addText("/admin → New Agent →   wizard or paste-blueprint mode", {
    x: 0.85, y: 1.85, w: 11.5, h: 0.5, fontFace: FONT_HEAD, fontSize: 18, color: C.CYAN, bold: true, margin: 0,
  });

  const steps = [
    { n: "①", t: "Name & owner",        d: "Tag the responsible business owner + sponsor",                c: C.ORANGE },
    { n: "②", t: "Identity mode",       d: "Delegated (OBO) · Application · Hybrid",                       c: C.PURPLE },
    { n: "③", t: "Permissions",         d: "Pick from Graph delegated + app permission catalog",           c: C.CYAN },
    { n: "④", t: "Knowledge sources",   d: "MCP endpoints, RAG stores, SharePoint, custom DB connectors",  c: C.MAGENTA },
    { n: "⑤", t: "Purview profile",     d: "Inherit org default OR pick agent-specific SITs",              c: C.ORANGE },
    { n: "⑥", t: "Model + deployment",  d: "Foundry deployment dropdown · region · token budget",         c: C.PURPLE },
    { n: "⑦", t: "Review JSON",         d: "Toggle to raw a365 manifest · git diff against last version", c: C.CYAN },
    { n: "⑧", t: "Submit + commit",     d: "Pushes blueprint, opens PR in your harness repo",              c: C.MAGENTA },
  ];
  let y = 2.4;
  for (const st of steps) {
    panel(s, 0.85, y, 11.7, 0.5);
    accentBar(s, 0.85, y, 0.5, st.c);
    s.addText(st.n, { x: 1.0, y, w: 0.6, h: 0.5, fontFace: FONT_HEAD, fontSize: 18, color: st.c, bold: true, valign: "middle", margin: 0 });
    s.addText(st.t, { x: 1.65, y, w: 2.7, h: 0.5, fontFace: FONT_HEAD, fontSize: 14, color: C.TEXT, bold: true, valign: "middle", margin: 0 });
    s.addText(st.d, { x: 4.4,  y, w: 8.1, h: 0.5, fontFace: FONT_BODY, fontSize: 13, color: C.MUTED, valign: "middle", margin: 0 });
    y += 0.55;
  }
}

// ============================================================
// SLIDE 22 — RECAP — what just happened
// ============================================================
{
  const s = newSlide("recap");
  title(s, "22", "What just happened, in one slide");

  panel(s, 0.6, 1.7, 12.2, 5.2);
  accentBar(s, 0.6, 1.7, 5.2, C.ORANGE);
  s.addText("Your existing harness, now governed", { x: 0.85, y: 1.85, w: 11.5, h: 0.6, fontFace: FONT_HEAD, fontSize: 24, color: C.TEXT, bold: true, margin: 0 });
  bullets(s, 0.85, 2.6, 11.5, 4.2, [
    "Two agents are running on this laptop — same as they would run in your harness",
    "Each has an Entra Agent ID minted from a Blueprint your security team can review",
    "Each prompt goes through Purview SDK or fallback regex SIT classifier — both directions",
    "Each call shows up in the Microsoft 365 admin center and in Defender for Cloud Apps",
    "You did not write authentication code — DefaultAzureCredential + OBO did it",
    "You did not write a DLP engine — Purview did it",
    "You did not change your harness's orchestrator — the SDK plugged into the existing seams",
  ], { fontSize: 16 });
}

// ============================================================
// SLIDE 23 — DECISION CHECKLIST
// ============================================================
{
  const s = newSlide("checklist");
  title(s, "23", "Five questions IT must answer before agents ship");

  const qs = [
    { q: "Who is the SPONSOR for this agent?",     a: "Lifecycle owner in admin center — visible to your auditor", c: C.ORANGE },
    { q: "What permissions does its BLUEPRINT list?", a: "Smallest set that meets the use case — review every change", c: C.PURPLE },
    { q: "Which SITs apply to its prompts?",       a: "Inherit org defaults + add agent-specific (e.g. account #, PII)", c: C.CYAN },
    { q: "Where does the SOC pivot?",              a: "Sentinel/Defender by AgentId; alerts on blueprint drift", c: C.MAGENTA },
    { q: "What's the kill switch?",                a: "M365 admin center → Suspend; revoke Agent ID in Entra", c: C.ORANGE },
  ];
  let y = 1.7;
  for (const q of qs) {
    panel(s, 0.6, y, 12.2, 1.0);
    accentBar(s, 0.6, y, 1.0, q.c);
    s.addText(q.q, { x: 0.85, y: y + 0.1,  w: 11.7, h: 0.45, fontFace: FONT_HEAD, fontSize: 17, color: q.c, bold: true, margin: 0 });
    s.addText(q.a, { x: 0.85, y: y + 0.55, w: 11.7, h: 0.4,  fontFace: FONT_BODY, fontSize: 14, color: C.TEXT, margin: 0 });
    y += 1.05;
  }
}

// ============================================================
// SLIDE 24 — GOVERNANCE SIGNALS YOU GET FOR FREE
// ============================================================
{
  const s = newSlide("signals");
  title(s, "24", "Governance signals you get for free");

  const signals = [
    "Sign-in event per agent token exchange",
    "Audit event per Graph call (incl. resource path)",
    "Sentinel event per Purview block",
    "Sign-in failure on conditional access deny",
    "Admin Center lifecycle change events",
    "Token consumption per agent per day",
    "Tool invocation telemetry (OTel)",
    "Blueprint drift alert (live perms ≠ manifest)",
    "Cost telemetry from Foundry deployment",
    "Anomaly detection from Defender for Cloud Apps",
  ];
  // 2-column 5x2 grid
  const colW = 5.8;
  const colH = 0.95;
  for (let i = 0; i < signals.length; i++) {
    const col = i % 2;
    const row = Math.floor(i / 2);
    const x = 0.6 + col * (colW + 0.4);
    const y = 1.7 + row * (colH + 0.1);
    panel(s, x, y, colW, colH);
    accentBar(s, x, y, colH, [C.ORANGE, C.PURPLE, C.CYAN, C.MAGENTA][i % 4]);
    s.addText(`${String(i + 1).padStart(2, "0")}`, {
      x: x + 0.15, y, w: 0.6, h: colH, fontFace: FONT_MONO, fontSize: 20, color: C.MUTED, bold: true, valign: "middle", margin: 0,
    });
    s.addText(signals[i], {
      x: x + 0.85, y, w: colW - 1.0, h: colH, fontFace: FONT_BODY, fontSize: 14, color: C.TEXT, valign: "middle", margin: 0,
    });
  }
}

// ============================================================
// SLIDE 25 — RISKS + MITIGATIONS
// ============================================================
{
  const s = newSlide("risks");
  title(s, "25", "Things to plan for");

  const rows = [
    { r: "Blueprint sprawl",      m: "GitOps the blueprints; require PR review from security + biz owner", c: C.ORANGE },
    { r: "Token-storm cost",      m: "Budget alerts per AgentId; Foundry deployment quotas",                 c: C.PURPLE },
    { r: "Purview replication lag", m: "5–10 min after policy create; document in runbook",                  c: C.CYAN },
    { r: "Local-auth temptation", m: "Set disableLocalAuth=true on Foundry — force Entra (we did)",          c: C.MAGENTA },
    { r: "Agent identity reuse",  m: "ONE Agent ID per use-case; never share across teams",                  c: C.ORANGE },
    { r: "Shadow MCP servers",    m: "Whitelist MCP endpoints in blueprint; egress controls",                c: C.PURPLE },
  ];
  let y = 1.7;
  for (const r of rows) {
    panel(s, 0.6, y, 12.2, 0.78);
    accentBar(s, 0.6, y, 0.78, r.c);
    s.addText(r.r, { x: 0.9, y, w: 4.0, h: 0.78, fontFace: FONT_HEAD, fontSize: 16, color: r.c, bold: true, valign: "middle", margin: 0 });
    s.addText(r.m, { x: 5.0, y, w: 7.7, h: 0.78, fontFace: FONT_BODY, fontSize: 14, color: C.TEXT, valign: "middle", margin: 0 });
    y += 0.88;
  }
}

// ============================================================
// SLIDE 26 — DAY-1 / DAY-30 / DAY-90 ROADMAP
// ============================================================
{
  const s = newSlide("roadmap");
  title(s, "26", "Your roadmap from here");

  const phases = [
    { t: "DAY 1",   items: ["Inventory existing agents", "Pick a sponsor model", "Mint blueprints for 2 pilot agents"], c: C.ORANGE },
    { t: "DAY 30",  items: ["Onboard all in-flight agents", "Define org-default Purview SITs for AI", "Wire Sentinel detection by AgentId"], c: C.PURPLE },
    { t: "DAY 90",  items: ["Blueprint GitOps + PR review SLA", "Cost telemetry dashboards", "Quarterly access review by sponsor"], c: C.CYAN },
  ];
  let x = 0.6;
  for (const p of phases) {
    panel(s, x, 1.7, 4.0, 5.2);
    accentBar(s, x, 1.7, 5.2, p.c);
    s.addText(p.t, { x: x + 0.25, y: 1.95, w: 3.6, h: 0.7, fontFace: FONT_MONO, fontSize: 28, color: p.c, bold: true, margin: 0 });
    s.addShape(pres.shapes.RECTANGLE, { x: x + 0.25, y: 2.7, w: 3.4, h: 0.02, fill: { color: p.c }, line: { type: "none" } });
    bullets(s, x + 0.25, 2.95, 3.6, 3.9, p.items, { fontSize: 15 });
    x += 4.15;
  }
}

// ============================================================
// SLIDE 27 — REPO LAYOUT
// ============================================================
{
  const s = newSlide("repo");
  title(s, "27", "Repo layout — what you take home");

  codeBlock(s, 0.6, 1.7, 12.2, 4.6, [
    "C:\\customagentharness\\",
    "├── apps\\",
    "│   ├── harness.tui\\               # Spectre.Console demo driver",
    "│   ├── harness.api\\               # Admin API + OTel SSE",
    "│   ├── harness.web\\               # Next.js admin + hub",
    "│   ├── ForgedAgentOne\\            # Delegated/OBO agent",
    "│   ├── ForgedScholarTwo\\          # App-perm + KB agent",
    "│   └── customagentharness-kb-mcp\\ # Local MCP server",
    "├── blueprints\\",
    "│   ├── forged-agent-one.harness.yaml  +  .a365.json",
    "│   └── forged-scholar-two.harness.yaml +  .a365.json",
    "├── shared\\CustomAgentHarness.Shared\\  # SDK wrapper + Purview wrapper",
    "├── kb\\agenticbank\\               # KB markdown corpus",
    "└── workshop\\                      # this deck, agenda, demo-script, leave-behind",
  ]);

  panel(s, 0.6, 6.5, 12.2, 0.6, C.CYAN);
  s.addText("$ harness demo --workshop      (Spectre TUI · 12 steps · --demo-mode fallback)", {
    x: 0.85, y: 6.5, w: 11.7, h: 0.6, fontFace: FONT_MONO, fontSize: 14, color: C.CYAN, bold: true, valign: "middle", margin: 0,
  });
}

// ============================================================
// SLIDE 28 — LEAVE-BEHIND
// ============================================================
{
  const s = newSlide("leave-behind");
  title(s, "28", "What you leave with");

  const items = [
    { t: "Workshop deck",   p: "workshop/slides.pptx",     d: "These slides (PPTX)",        c: C.ORANGE },
    { t: "Demo script",     p: "workshop/demo-script.md",  d: "Step-by-step presenter guide", c: C.PURPLE },
    { t: "Run-of-show",     p: "workshop/agenda.md",       d: "3-hour timing + pre-flight",  c: C.CYAN },
    { t: "One-pager",       p: "workshop/leave-behind.md", d: "Exec + technical handout (print)", c: C.MAGENTA },
    { t: "Architecture",    p: "workshop/architecture.excalidraw", d: "Editable system diagram", c: C.ORANGE },
    { t: "Blueprints",      p: "blueprints/*.yaml",        d: "Reference YAML you can fork",  c: C.PURPLE },
    { t: "Sample harness",  p: "apps/**",                  d: "Both agents · KB MCP · TUI · admin/hub UI", c: C.CYAN },
  ];
  let y = 1.7;
  for (const it of items) {
    panel(s, 0.6, y, 12.2, 0.65);
    accentBar(s, 0.6, y, 0.65, it.c);
    s.addText(it.t, { x: 0.9, y, w: 2.6, h: 0.65, fontFace: FONT_HEAD, fontSize: 15, color: it.c, bold: true, valign: "middle", margin: 0 });
    s.addText(it.p, { x: 3.6, y, w: 4.8, h: 0.65, fontFace: FONT_MONO, fontSize: 13, color: C.CYAN, valign: "middle", margin: 0 });
    s.addText(it.d, { x: 8.5, y, w: 4.2, h: 0.65, fontFace: FONT_BODY, fontSize: 13, color: C.MUTED, valign: "middle", margin: 0 });
    y += 0.75;
  }
}

// ============================================================
// SLIDE 29 — Q&A
// ============================================================
{
  const s = newSlide("q&a");

  // big "?" centered
  s.addText("?", {
    x: 0.5, y: 1.2, w: 12.3, h: 3.5,
    fontFace: FONT_MONO, fontSize: 280, color: C.ORANGE, bold: true,
    align: "center", valign: "middle", margin: 0,
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 4.65, y: 4.85, w: 4.0, h: 0.03, fill: { color: C.CYAN }, line: { type: "none" },
  });
  s.addText("Questions, doubts, push-back", {
    x: 0.5, y: 5.0, w: 12.3, h: 0.6,
    fontFace: FONT_HEAD, fontSize: 28, color: C.TEXT, bold: true, align: "center", margin: 0,
  });
  s.addText("...and the awkward ones especially.", {
    x: 0.5, y: 5.65, w: 12.3, h: 0.5,
    fontFace: FONT_BODY, fontSize: 16, color: C.MUTED, italic: true, align: "center", margin: 0,
  });

  s.addText("admin@example.org  ·  github.com/<your-org>/customagentharness", {
    x: 0.5, y: 6.6, w: 12.3, h: 0.4,
    fontFace: FONT_MONO, fontSize: 13, color: C.CYAN, align: "center", margin: 0,
  });
}

// ============================================================
// SLIDE 30 — THANK YOU
// ============================================================
{
  const s = newSlide("end");

  s.addText("// thank you", {
    x: 0.5, y: 1.5, w: 12.3, h: 0.6,
    fontFace: FONT_MONO, fontSize: 24, color: C.CYAN, align: "center", margin: 0,
  });
  s.addText("Ship your agents.", {
    x: 0.5, y: 2.4, w: 12.3, h: 1.2,
    fontFace: FONT_HEAD, fontSize: 60, color: C.TEXT, bold: true, align: "center", margin: 0,
  });
  s.addText("Govern them like apps.", {
    x: 0.5, y: 3.6, w: 12.3, h: 1.2,
    fontFace: FONT_HEAD, fontSize: 60, color: C.ORANGE, bold: true, align: "center", margin: 0,
  });
  s.addText("Audit them like users.", {
    x: 0.5, y: 4.8, w: 12.3, h: 1.2,
    fontFace: FONT_HEAD, fontSize: 60, color: C.PURPLE, bold: true, align: "center", margin: 0,
  });

  // signature bar
  s.addShape(pres.shapes.RECTANGLE, {
    x: 4.0, y: 6.4, w: 5.3, h: 0.04, fill: { color: C.CYAN }, line: { type: "none" },
  });
  s.addText("CustomAgentHarness  ×  Microsoft Agent 365", {
    x: 0.5, y: 6.55, w: 12.3, h: 0.4,
    fontFace: FONT_MONO, fontSize: 14, color: C.MUTED, align: "center", margin: 0,
  });
}

// ============================================================
// WRITE
// ============================================================
pres.writeFile({ fileName: "C:/customagentharness/workshop/slides.pptx" })
  .then(f => { console.log("Wrote", f, "with", SLIDE_NO, "slides"); })
  .catch(err => { console.error("Failed:", err); process.exit(1); });
