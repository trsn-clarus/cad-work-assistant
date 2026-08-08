# CAD Work Assistant Design System

## Direction

CAD Work Assistant is a professional desktop tool for AutoCAD-connected quantity workflows. The interface should feel calm, precise, dense, and fast. It should look closer to CAD, engineering, and inspector-based productivity software than a SaaS landing page.

## UI UX Pro Max Inputs

- Query used: `engineering desktop software CAD application data dense interface`
- Adopted recommendations: data-dense dashboard structure, compact tables, hover row highlighting, clear loading and feedback states, visible focus states, keyboard navigation, tabular numeric presentation.
- Rejected recommendations: neon cyan, interference purple, magenta CTA, marketing landing structure. Those conflict with the CAD Work Assistant requirement for a calm technical workspace.
- Milestone 3 (Area): no separate UI UX Pro Max / 21st.dev session was available in this environment. Per the documented fallback order (design system → UI UX Pro Max → existing Length UI → general CAD/engineering UX judgment), Area reused this record's findings plus the already-shipped Length panel as its direct reference rather than re-deriving them - see `pages/measurement-workspace.md` for the resulting shared structure and the reasoning for keeping exclusions in one summary sentence instead of a per-row status column.
- Milestone 4.5 (Production UI/UX pass): ran 8 `ui-ux-pro-max` queries (`--design-system`, `--domain style/color/typography/ux`, `--stack wpf`) against the shipped UI rather than the empty shell. Adopted: density tokens (8px grid gap, 12px card padding, 36px table rows) validated the existing spacing scale almost exactly; the navy/slate/blue "B2B service" and "developer tool" color families validated the existing Accent/Background palette, so no re-theme was needed. Rejected: dark-mode-first palettes (this milestone explicitly does not add Dark Mode), Inter/JetBrains Mono font swaps (Segoe UI/Cascadia Mono already serve the desktop-native goal without a new font dependency), CommunityToolkit.Mvvm (already a settled architecture decision, out of scope), blur-only validation for numeric inputs (Vertical Area/Parapet's live-recalculate-on-keystroke behavior from Milestone 4 is intentional). No 21st MCP tool was available in this environment either (`ToolSearch` returned nothing under that name) - the documented fallback order applied for the whole pass.

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

Recommended sections:

- PROJECT: Dashboard, Files
- CAD: Drawing, Selection, Layers, Export
- QUANTITY: Length, Area, Vertical Area, Parapet, History
- OUTPUT: Plot, PDF, Excel
- SETTINGS: Preferences

Only nav items with a real screen behind them are enabled (`NavItem.IsImplemented=true`): Dashboard,
Length, Area, Vertical Area, Parapet. The rest render at reduced opacity, are not clickable, and show
a "(곧 제공됩니다)" tooltip - reserving the item's place without pretending it does something today.

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
