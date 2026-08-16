---
name: Toner
description: Operations board for printer rental and field service — split-flap concourse language, not a SaaS dashboard.
colors:
  board-bg: "#101317"
  board-panel: "#1a2026"
  board-panel-raised: "#21282f"
  board-seam: "#5b6672"
  board-seam-soft: "#262e35"
  flap-ink: "#eef0ec"
  flap-ink-dim: "#99a1ab"
  signal-blue: "#2760d1"
  signal-blue-bright: "#5b8dff"
  signal-amber: "#d9a441"
  signal-red: "#d64545"
typography:
  display:
    fontFamily: "'InterVariable', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
    fontSize: "1.4rem"
    fontWeight: 700
    lineHeight: 1.1
    letterSpacing: "0.05em"
  body:
    fontFamily: "'InterVariable', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
    fontSize: "0.9rem"
    fontWeight: 400
    lineHeight: 1.5
    letterSpacing: "normal"
  label:
    fontFamily: "'InterVariable', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
    fontSize: "0.72rem"
    fontWeight: 600
    lineHeight: 1
    letterSpacing: "0.04em"
rounded:
  none: "0px"
spacing:
  sm: "0.5rem"
  md: "1rem"
  lg: "1.5rem"
components:
  button-primary:
    backgroundColor: "{colors.signal-blue}"
    textColor: "#ffffff"
    rounded: "{rounded.none}"
    padding: "0.65rem 1rem"
  nav-lane-active:
    backgroundColor: "{colors.signal-blue}"
    textColor: "{colors.flap-ink}"
    rounded: "{rounded.none}"
---

# Design System: Toner

## Overview

**Creative North Star: "The Split-Flap Concourse Board"**

Toner's operations shell reads as a departures board, not a SaaS admin template. Every list is a live board where a row's state — on time, at risk, cancelled — is the thing the page exists to show, not a decoration bolted onto a card grid. The system won this world in Impeccable's own direction roll: the catalog challenger "Rail concourse split-flap board" was weighed against an assigned synoptic-weather-board candidate on two axes (audience identification, product clarity) and won both, on the strength of a real pending product need — real-time status — that the board's clatter-and-flip grammar answers directly.

The palette is Restrained: a near-black board ground and warm-white "flap" ink carry the surface, with one signal-blue accent for interactive and on-time state. Amber and red are not decoration; they are the board's own delay/cancellation lamps, functionally reserved. State is never color-only: every status pairs a lamp *shape* (filled circle, rotated diamond, hollow ring) with a text label.

This is an Operate-mode surface for a five-role internal tool (Administrador, Coordinador, Técnico, Cliente, Ventas): scanability and native table/form affordances outrank expression. The board's one authored motion — a value clattering through a short random run before landing, via `FlapText.vue` — is spent only on the login wordmark and dashboard KPI values; it is never scattered onto ordinary UI transitions, and it is fully suppressed under `prefers-reduced-motion`.

**Confirmed visual rejections:** the generic SaaS admin default (gradient sidebar, rounded card grid, soft shadows) was presented as the standing "category standard" exit and explicitly not chosen. The hero-metric card grid (icon + big number + label, same-size cards) that shipped in the pre-redesign dashboard is retired; craft-floor bans it as the category default.

**Key Characteristics:**
- Near-black board ground, zero corner radius anywhere
- One signal-blue accent; amber/red are reserved status lamps, not decoration
- One typeface (Inter) throughout; weight, case, and tracking carry hierarchy instead of a second "theme" display face
- Status is always lamp-shape + color + text label together, never color alone
- Hairline seams (1px dividers) do the work cards and shadows usually do
- No sequence numbers on nav items, form fields, or KPIs — an icon and a label are their own identity; a `01 / 02 / 03` badge in front of everything was tried and cut as a costume, not a function

## Colors

Restrained strategy: neutrals carry the surface, one accent (signal-blue) marks interactivity and "on schedule," and two reserved status colors (amber, red) exist only as lamps.

### Primary
- **Signal Blue** (`#2760d1`, solid fills — buttons, active nav lane background, `el-color-primary`): the one accent. Marks interactive elements and the "on time / live" board state.
- **Signal Blue Bright** (`#5b8dff`, text/icon use on dark ground): the active nav icon, the "En línea" status dot, links, focus rings. Used instead of the solid fill wherever the color sits on text/an icon rather than a filled shape (contrast-driven split, not a second accent).

### Neutral
- **Board Ground** (`#101317`): page background. Committed dark, not a togglable dark mode — chosen because the split-flap board itself is physically dark with lit lettering, which also happens to read well for técnicos checking the app outdoors in bright daylight.
- **Board Panel** (`#1a2026`): the surface for every panel, table, and card.
- **Board Panel Raised** (`#21282f`): hover state for rows and the mobile drawer's header.
- **Board Seam** (`#5b6672`, ≥3:1 against ground): functional borders — input borders, dividers that carry meaning (WCAG 1.4.11 non-text contrast).
- **Board Seam Soft** (`#262e35`): decorative dividers — panel borders, table header rules, KPI-lane separators.
- **Flap Ink** (`#eef0ec`): primary text (15.7:1 on ground).
- **Flap Ink Dim** (`#99a1ab`): secondary/caption text (6.9:1 on ground).

### Status lamps (reserved, not decorative accents)
- **Signal Amber** (`#d9a441`): "at risk" / delayed. Rendered as a rotated-diamond lamp plus text label.
- **Signal Red** (`#d64545`): "critical" / cancelled / overdue. Rendered as a hollow-ring lamp plus text label. Element Plus's own `success`/`warning`/`danger` toast semantics were deliberately left close to their conventional green/amber/red meaning — those are a different, universally-anchored vocabulary (form validation, save confirmations) from the board's own domain-status lamps, and blending the two would cost more clarity than the "more blue" brief gained.

### Named Rules
**The Never-Color-Alone Rule.** Every status (`LaneStatus.vue`) pairs its color with a distinct lamp shape (filled circle / rotated diamond / hollow ring) and a text label. A status is never legible by hue alone.

## Typography

**Display Font:** Inter (variable), with the system UI stack as fallback
**Body Font:** Inter (variable), with the system UI stack as fallback
**Character:** One family carries the whole system. An earlier pass paired Inter with a condensed industrial-signage grotesk (Big Shoulders) for chrome; it read as a costume borrowed to sell the "airport board" metaphor rather than considered typography, and was cut. Hierarchy is now weight (400 body / 600–700 emphasis), case (uppercase + tracking for structural labels — nav, table headers, status lamps), and size (the scale below), never a second face.

### Type scale

Every font-size in the board world resolves to one of these nine CSS custom properties (`--text-*`, defined in `board-theme.css`); no view sets a raw font-size literal.

| Token | Value | Used for |
|---|---|---|
| `--text-2xs` | 0.72rem | Table column headers, status-lamp labels, KPI-lane captions |
| `--text-xs` | 0.75rem | Small labels/captions — header status strip, form field lane-labels, welcome eyebrow |
| `--text-sm` | 0.8rem | Secondary text — empty states, nav lane numbers, KPI sub-captions, inline errors |
| `--text-md` | 0.85rem | Panel titles, login subtitle, nav lane item labels |
| `--text-lg` | 1.15rem | Sidebar brand mark |
| `--text-xl` | 1.4rem | Page/section titles |
| `--text-2xl` | 1.8rem | Non-staff welcome title |
| `--text-display` | 2rem | Dashboard KPI values |
| `--text-display-lg` | 2.25rem | Login wordmark |

### Named Rules
**The One-Voice Rule.** Inter is the only typeface in the system. Hierarchy comes from weight, case/tracking, and the scale below — never a second "theme" font, however well it matches the metaphor.
**The Token-Only Rule.** Font sizes are always `var(--text-*)`; a literal `font-size` value in board-world CSS is drift, not a new step — add a named step to the scale first if a genuinely new size is needed.
**The No-Numbering Rule.** Nav items, KPI lanes, and form fields are identified by icon + label (or label alone), never by a sequential `01 / 02 / 03` badge. A number is earned only when the sequence itself is information the reader needs (e.g. a step-by-step wizard); decorative sequencing was cut as an AI-generated-design tell.

## Layout

The shell is a fixed rail (`AppLayout.vue`) plus a fluid content column, both `el-container`-based.

- **Desktop (≥768px):** sidebar rail is 248px expanded / 68px icon-only, user-toggleable, persisted to `localStorage`.
- **Narrow (<768px):** the rail becomes an off-canvas drawer (`transform: translateX`, not width — width/height/padding/margin transitions are avoided project-wide per the design-detector's layout-thrash rule), hidden by default behind a header hamburger, with a backdrop click-to-close and `inert` applied while closed so it drops out of the tab order and accessibility tree.
- Dashboard KPI panel: 4-column grid → 2 columns at ≤960px → 1 column at ≤640px, divided by hairline seams rather than gutters between separate cards.
- Two-up panel rows (`board-grid`) collapse to a single column at ≤960px.
- Every flex/grid container in the shell carries `min-width: 0` on its children so a dense table scrolls inside its own panel instead of blowing out the page into horizontal scroll.
- Content padding: 1.5rem desktop, 1rem at ≤768px.

## Elevation & Depth

Flat by default: panels are distinguished from the ground by a single hairline seam border, not shadow. The two exceptions are real overlays — the login gate card and the mobile nav drawer — which use a soft, offset shadow to read as physically lifted above the board.

### Shadow Vocabulary
- **Overlay** (`box-shadow: 0 24px 48px rgba(0,0,0,0.45)` on the login card; `0 0 32px rgba(0,0,0,0.5)` on the mobile drawer): reserved for elements that sit above the board plane, never for ordinary panels.

### Named Rules
**The Seam-Not-Shadow Rule.** A panel at rest is separated from its neighbors by a 1px seam, never a shadow. Shadow is reserved for things that are actually floating above the board (modals, the mobile drawer, the login card).

**Scrim** (`--scrim: rgba(6, 7, 9, 0.6)`): the backdrop behind anything floating above the board — currently the mobile nav drawer's backdrop. Any future modal/overlay reuses this token rather than a new literal.

## Shapes

Zero corner radius anywhere in the shell — panels, buttons, inputs, the login card, and status lamps (aside from the circular/diamond lamp glyphs themselves) are all square-cornered, matching a physical flap board's machined edges. A rotated square (diamond) and a ring are the only non-rectangular, non-circular forms in the system, both reserved for the status-lamp vocabulary.

## Components

### Navigation (sidebar rail)
- Each item is an icon plus label — no sequence number (see Typography's No-Numbering Rule).
- **Active state:** a Signal Blue wash fills the whole row (`rgba(47,111,237,0.16)`); the icon brightens to Signal Blue Bright and the label brightens to Flap Ink. No colored left border is used for active state (craft-floor bans a colored border-left/right >1px as a category default device).
- **Mobile:** off-canvas drawer, see Layout.

### Buttons
- **Shape:** square corners (0 radius).
- **Primary:** Signal Blue fill (`#2760d1`), white text.
- **Text/link buttons** (collapse toggle, logout, drawer close): Flap Ink Dim, brightening to Flap Ink or Signal Red (logout, on hover) on interaction.

### Board Panel (the system's card equivalent)
- **Corner style:** square.
- **Background:** Board Panel (`#1a2026`).
- **Border:** 1px Board Seam Soft.
- **Header:** condensed uppercase label row, Flap Ink Dim, bottom hairline.
- No nested cards; a panel's internal divisions are hairline seams (see KPI panel), never cards-within-cards.

### KPI Lanes (signature component)
Four lanes inside one panel, divided by vertical hairline seams rather than four separate card boxes — the direct replacement for the banned "icon + big number + label" card-grid pattern. Each lane: an uppercase label, a `FlapText`-animated value in display weight, a one-line caption in Flap Ink Dim.

### Status Lamp (`LaneStatus.vue`, signature component)
Inline lamp-shape + uppercase label. `on-time` → filled circle, Signal Blue Bright. `warning` → rotated-diamond, Signal Amber. `critical` → hollow ring, Signal Red. `idle` → small filled circle, Board Seam. Always both shape and text; see Named Rule under Colors.

### Flap Text (`FlapText.vue`, signature component)
The system's one authored motion. On mount (or value change), each character clatters through 3–5 same-alphabet random glyphs before settling, staggered left-to-right. Fully inert under `prefers-reduced-motion`; the real value is always present as a visually-hidden accessible string regardless of animation state. Reserved for the login wordmark and dashboard KPI values only.

### Inputs / Fields
- Square corners, Board Seam border, Board Panel Raised-adjacent focus ring in Signal Blue Bright (`outline: 2px solid`).
- Labels are plain product copy (`Cédula`, `Contraseña`) — no numbering, no custom label chrome beyond Element Plus's own dark-themed label styling.

### Tables
- Transparent header/body background (inherits Board Panel), condensed uppercase column headers, tabular numerals (`font-variant-numeric: tabular-nums`).
- Row hover: Board Panel Raised.
- Horizontal overflow scrolls inside the table's own wrapper; the page itself never scrolls horizontally (see Layout's `min-width: 0` rule).

## Do's and Don'ts

### Do:
- **Do** pair every status color with a distinct lamp shape and a text label (The Never-Color-Alone Rule).
- **Do** use Signal Blue as the only interactive/brand accent; keep amber and red reserved for at-risk/critical status lamps.
- **Do** set every weight of hierarchy in Inter; use weight, case/tracking, and the `--text-*` scale to differentiate, never a second face.
- **Do** separate panels with a 1px Board Seam Soft hairline at rest; reserve shadow for genuine overlays (login card, mobile drawer).
- **Do** keep every corner square; a rounded corner anywhere in the shell is a system violation.
- **Do** give any flex/grid container that might host a wide table `min-width: 0` so it scrolls inside its own panel instead of forcing page-level horizontal scroll.
- **Do** animate the rail drawer (or any open/close chrome) via `transform`, never `width`/`height`/`padding`/`margin`.

### Don't:
- **Don't** reintroduce the icon + big-number + label card grid for KPIs; use hairline-divided lanes inside one panel instead.
- **Don't** use a colored `border-left`/`border-right` to mark an active nav item or list row; use a background wash instead.
- **Don't** add a second brand accent color. Amber and red are status lamps, not alternate accents, and are never used decoratively.
- **Don't** add a second display typeface. Big Shoulders was tried for the "airport board" metaphor and cut for reading as a costume, not typography (The One-Voice Rule).
- **Don't** prefix nav items, form fields, or KPI labels with a sequence number; an icon/label pair carries identity on its own (The No-Numbering Rule).
- **Don't** apply the `FlapText` clatter motion outside the login wordmark and dashboard KPI values; it is a reserved, budgeted signature moment, not a general entrance effect.
- **Don't** leave the mobile drawer focusable while visually closed; it must carry `inert` (or equivalent) whenever it is off-canvas.
