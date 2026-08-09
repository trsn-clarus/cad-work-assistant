# CAD Work Assistant Design System

## Direction

CAD Work Assistant is a professional desktop tool for AutoCAD-connected quantity workflows. The interface should feel calm, precise, dense, and fast. It should look closer to CAD, engineering, and inspector-based productivity software than a SaaS landing page.

## UI UX Pro Max Inputs

- Query used: `engineering desktop software CAD application data dense interface`
- Adopted recommendations: data-dense dashboard structure, compact tables, hover row highlighting, clear loading and feedback states, visible focus states, keyboard navigation, tabular numeric presentation.
- Rejected recommendations: neon cyan, interference purple, magenta CTA, marketing landing structure. Those conflict with the CAD Work Assistant requirement for a calm technical workspace.
- Milestone 3 (Area): no separate UI UX Pro Max / 21st.dev session was available in this environment. Per the documented fallback order (design system → UI UX Pro Max → existing Length UI → general CAD/engineering UX judgment), Area reused this record's findings plus the already-shipped Length panel as its direct reference rather than re-deriving them - see `pages/measurement-workspace.md` for the resulting shared structure and the reasoning for keeping exclusions in one summary sentence instead of a per-row status column.
- Milestone 4.5 (Production UI/UX pass): ran 8 `ui-ux-pro-max` queries (`--design-system`, `--domain style/color/typography/ux`, `--stack wpf`) against the shipped UI rather than the empty shell. Adopted: density tokens (8px grid gap, 12px card padding, 36px table rows) validated the existing spacing scale almost exactly; the navy/slate/blue "B2B service" and "developer tool" color families validated the existing Accent/Background palette, so no re-theme was needed. Rejected: dark-mode-first palettes (this milestone explicitly does not add Dark Mode), Inter/JetBrains Mono font swaps (Segoe UI/Cascadia Mono already serve the desktop-native goal without a new font dependency), CommunityToolkit.Mvvm (already a settled architecture decision, out of scope), blur-only validation for numeric inputs (Vertical Area/Parapet's live-recalculate-on-keystroke behavior from Milestone 4 is intentional). No 21st MCP tool was available in this environment either (`ToolSearch` returned nothing under that name) - the documented fallback order applied for the whole pass.
- Milestone 6 (Project Management UI): no new `ui-ux-pro-max`/21st session was run - the master prompt explicitly required reusing the Milestone 4.5 Production Design System without inventing new patterns (no new "SaaS-like" project-picker UI). The `ProjectDialog` window and sidebar project switcher reuse existing tokens/styles verbatim (`PanelBorder`, `PrimaryButton`/`SecondaryButton`, `CaptionText`/`SectionTitle`, `InlineMessageBorder`/`InlineMessageText`, the implicit `TextBox` style) - see `pages/project-management.md`.
- Milestone 7 (Quantity History + Verification): no new session - the master prompt explicitly warned against web SaaS pill badges and floating-card verification UIs (§107, §112-114), asking instead for compact glyph+text status communication matching the existing engineering-table density. `HistoryPanel` reuses the `DrawingPanel` two-pane layout (list `*` / `GridSplitter` / detail pane, Milestone 5) verbatim rather than inventing a new "audit log" or "review queue" visual pattern, and its detail pane reuses the Inspector section-header convention (`SectionTitle` + label/value rows) instead of a new card component - see `pages/quantity-history.md`.
- Milestone 8 (Production Packaging + Premium UI/UX Finalization): ran 5 `ui-ux-pro-max` queries (`--design-system`, `--stack wpf`, `--domain color/style/ux`) against the shipped UI as a validation pass, not a redesign - by this point the app had already been through four production UI passes (4.5/5/6/7). Adopted: nothing new (the navy/slate palette, dense spacing, empty-state pattern, checkbox+action-bar bulk selection, and active-nav highlighting the queries surfaced were all already implemented, so they served as confirmation rather than new direction). Rejected: the `--design-system` query's "Enterprise Gateway" web-landing pattern (hero/mega-menu/contact-sales - marketing structure, not a workspace), CommunityToolkit.Mvvm (settled architecture decision, same as Milestone 4.5), and all three `style`-domain results (HUD/Sci-Fi neon, Bitcoin DeFi glassmorphism+gradients, minimal landing page) - none matched this product and the neon/glassmorphism/gradient elements are this project's own documented anti-patterns. 21st.dev CLI was re-verified reachable but still requires `21st login`/`TWENTYFIRST_TOKEN` with no secret available in this session (same conclusion as every prior milestone) - reused the IA patterns already recorded in `.21st/DESIGN.md` instead. The actual UI audit was done by running the app in Simulation Mode and screenshotting it, not by researching further - see `design-system/PRODUCTION_UI_REVIEW.md` for the full findings, adopted/rejected list, and every change made.
- Milestone 9 (Excel Quantity Export): no new `ui-ux-pro-max`/21st session - the new Excel screen is a form (scope radio + checkboxes + summary + one primary button) using only tokens already established (`PanelBorder`, `PrimaryButton`, `CaptionText`/`SectionTitle`, radio/checkbox implicit styles, `InlineMessageBorder`/`InlineMessageText` for the Success/Error states), and it is also the first screen to open a native `Microsoft.Win32.SaveFileDialog` rather than an in-app dialog - see `pages/excel-export.md`. The Excel *document* itself (not the screen) reuses the app's own color tokens translated to ClosedXML cell styles rather than any spreadsheet-template research, documented in `docs/EXCEL_EXPORT.md` §12 rather than here since it is document styling, not WPF UI.
- Milestone 10 (PDF Quantity Report): no new `ui-ux-pro-max`/21st session - the PDF screen is deliberately the same form shape as Excel's (scope radio + checkboxes + summary + one primary button), reusing every token Milestone 9 already established rather than inventing a second visual pattern for what is functionally the same kind of task - see `pages/pdf-export.md`. The PDF *document* itself translates the same design-system palette into MigraDoc styles (documented in `docs/PDF_EXPORT.md` §9, not here, for the same reason as Milestone 9's Excel styling). The one new finding worth recording here rather than in the docs: Simulation Mode testing caught an `AutomationProperties.Name` on the PDF panel's primary button that didn't match its visible `Content` binding (`"PDF 파일 생성"` vs. the rendered `"PDF 보고서 생성"`) - this is a real accessibility defect (a screen reader announces different text than what's on screen), not just a UI-Automation test inconvenience, and was fixed to match.

## 21st Inputs

- CLI verified with `npx @21st-dev/cli@latest --help`.
- Design context generated with `npx @21st-dev/cli@latest init --design-context --refresh`.
- Component search currently requires `21st login` or `TWENTYFIRST_TOKEN`; no secret was stored in source.
- Pattern guidance used conceptually: sidebar navigation, data table, property inspector, status bar, command palette, activity log.

## Colors

| Token | Value | Use |
| --- | --- | --- |
| Background | `#F3F5F7` | App workspace background |
| Surface | `#FFFFFF` | Main panels and controls |
| Surface Muted | `#EEF2F5` | Headers, quiet sections |
| Surface Raised | `#FAFBFC` | Sidebar/status surfaces |
| Border | `#CAD3DC` | Default separators |
| Border Strong | `#9AA8B6` | Hover and emphasized separators |
| Divider | `#DCE3E9` | Lighter separators inside a single panel (e.g. Parapet 측면/상부 breakdown) |
| Text Primary | `#17212B` | Primary text |
| Text Secondary | `#526170` | Labels, hints, metadata |
| Text Disabled | `#94A3AF` | Disabled control text (distinct from muted so a future re-theme doesn't hunt through TextMuted usages) |
| Accent | `#1D6F8F` | Primary action fill, active nav indicator |
| Accent Hover | `#175A73` | `PrimaryButton` hover background - never `SurfaceRaised`, see Components below |
| Accent Pressed | `#124657` | `PrimaryButton` press background |
| Success | `#1E7A47` | Completed calculation states |
| Warning | `#946200` | Recoverable problems (awaiting selection, invalid input) |
| Error | `#B42318` | Blocking failures |
| Selection | `#CFE7F0` | Selected table rows |
| Focus | `#0B83A5` | Keyboard focus |

Connection state (`BrushConnected`/`BrushConnecting`/`BrushDisconnected`/`BrushConnectionError`) is a
separate semantic alias set layered on top of Success/Warning/TextMuted/Error - same values today, but
named for "connection", not "success/error", so a future re-theme of connection status alone doesn't
require auditing unrelated Success/Error usages. See `ConnectionStatusGlyph` in Components below for
why color is never the only signal for connection state.

## Typography

- Sans: Segoe UI for native Windows readability.
- Numeric: Cascadia Mono, Consolas for quantities, coordinates, lengths, areas, and IDs.
- Use small, compact headings inside panels. Avoid hero-scale type in the workspace.
- Numeric values should use stable widths wherever repeated measurement values are shown.
- Value/unit hierarchy: a result total is two `TextBlock`s, not one string - the value uses
  `NumericText` at its full size/weight, the unit uses `NumericUnitText` (same mono font, normal
  weight, `TextMuted`) one step down. This applies to the one "hero" total per measurement panel
  (Length/Area/Vertical Area/Parapet's grand total); per-line breakdown numbers (e.g. Parapet's
  측면/상부 rows) stay as combined value+unit text - splitting every number in a table would fight
  density instead of serving it.

## Spacing And Density

- Base unit: 4 px.
- Compact component spacing: 4, 8, 12, 16.
- Workspace spacing: 12, 14, 18, 24.
- Tables should prioritize scan speed: 32 px headers and 34 px rows.
- Avoid nested cards. Use panels for major regions and cards only for small repeated metrics.

## Radius, Border, Elevation

- Radius small: 3 px.
- Radius medium: 6 px.
- Borders are the primary hierarchy tool.
- Shadows should be rare; use them only for modal separation if needed.

## Layout

- Use a three-region workspace: sidebar, main workspace, inspector.
- Keep a persistent status bar with AutoCAD connection, active DWG, units, selection, and background operation status.
- Desktop is the primary environment. Tablet and mobile layouts may stack secondary panels, but desktop density must not be sacrificed.

## Navigation

Current sections (as of Milestone 10):

- PROJECT: Dashboard
- CAD: Drawing
- QUANTITY: Length, Area, Vertical Area, Parapet, History
- OUTPUT: Excel, PDF
- SETTINGS: Settings

Only nav items with a real screen behind them appear in `Navigation` at all. Through Milestone 7
this list also carried placeholder entries (Files, Plot, PDF, Excel, Preferences) rendered at
reduced opacity and disabled, reserving their eventual place. Milestone 8 removed that pattern
project-wide: a production first impression with five permanently-unclickable nav items reads worse
than a shorter, fully-functional list. Milestone 9 was the first of those placeholders to become a
real screen - a new OUTPUT group with one item, Excel. Milestone 10 added PDF as OUTPUT's second
item, right below Excel (no new group header - `ShowGroupHeader` only fires once per group, the
same rule History/Area/Vertical Area/Parapet already follow under QUANTITY). Files/Plot get real
screens in a future Milestone (Plot is Milestone 11, and is a different kind of PDF entirely -
drawing plot output, not this quantity report - see `pages/pdf-export.md`) and get added then -
not before.

## Button hierarchy

Not every button is Accent-filled. Three tiers, each its own `ControlTemplate` (see "Why three
templates" below):

- **Primary** - opaque Accent fill, white text. One per screen: the core action that starts a
  workflow (`CAD에서 객체 선택` etc.).
- **Secondary** - Accent-outlined, unfilled. Confirmatory/completion actions that sit next to a
  Primary button without competing for attention (`산출내역 추가`).
- **Quiet** - no border, muted text, subtle hover fill. Tertiary actions that are always available
  but shouldn't draw the eye (`값 복사`).

**Why three separate templates, not one shared template with variant colors**: an earlier version had
`PrimaryButton` inherit the neutral `Button` template's hover/press triggers. Those triggers set
`Background` to `BrushSurfaceRaised` (near-white) on hover regardless of which style applied them,
which silently turned Primary's white foreground text illegible the moment the mouse moved over it.
Confirmed by an actual screenshot during Milestone 4.5's visual audit, not just code review. Fixed by
giving `PrimaryButton` (and `SecondaryButton`/`QuietButton`) their own `ControlTemplate` whose
hover/press states only move within that variant's own color family (Accent → AccentHover →
AccentPressed for Primary).

## Components

- Sidebar navigation: grouped, compact, keyboard reachable, unimplemented items disabled not hidden
  (see Navigation above).
- Command palette: `Ctrl+K`, searchable commands, closes with `Esc`. Every entry must be a real,
  wired command - an "Extract Length" entry with no bound behavior beyond faking a status message
  was removed in Milestone 4.5 rather than kept as a placeholder.
- Data table: virtualized, row hover, selected row, long drawing names handled by trimming.
- Property inspector: a live view of whichever QUANTITY tool is active, not a static mock. Length/Area
  show state/object count/layer summary/total pulled straight from that tool's `Rows` + `TotalDisplay`;
  Vertical Area/Parapet show the resolved base length/layer (via `LengthSourceSelector`), height, and
  total; the Dashboard (no tool active) shows connection state, active drawing, quantity record count,
  and the most recent activity entry. There is no "Calculation Mode/Rounding" or "Open Drawings"
  section anymore - both were static content with nothing behind them.
- Status bar: connection (glyph + label, see Accessibility), current DWG, units, selection.
- Activity log: timestamped feedback, not generic spinners. Populated only by real events (a
  `RecordAdded` from any of the four measurement tools); starts empty with an action-oriented empty
  state, never seeded with sample rows.

## Motion

- Use WPF native animation only for state changes: command palette opening and inspector reveal.
- Keep timing around 120-180 ms.
- Motion must not move surrounding layout or block input.
- Respect `SystemParameters.ClientAreaAnimation`; when disabled, skip added motion.

## Accessibility

- All actions must be reachable by keyboard.
- Keep visible focus states.
- Use semantic WPF controls instead of clickable text.
- Add `AutomationProperties.Name` for icon-only or ambiguous controls.
- Do not convey status by color alone; include text labels such as Connected, Warning, Success.
- Connection state additionally gets its own glyph, not just a colored dot: `●` Connected,
  `◐` Connecting/Reconnecting, `◇` Detected (awaiting instance selection), `△` Plugin not loaded,
  `✕` Disconnected, `!` Error, `○` Not running (`ConnectionStatusGlyph`). Applied in both the sidebar
  AutoCAD card and the bottom status bar.
- Every button whose content isn't plain text (icon-only, or a `Grid` combining a label with a
  shortcut hint) needs an explicit `AutomationProperties.Name` - WPF's default automation name falls
  back to `Content.ToString()`, which for a `Grid` produces something unreadable to a screen reader.

## Anti-Patterns

- Purple AI gradients.
- Neon glow.
- Glassmorphism.
- Oversized hero sections.
- Decorative 3D effects.
- Large marketing cards.
- Fade-in animation for every element.
- Mouse-only workflows.
