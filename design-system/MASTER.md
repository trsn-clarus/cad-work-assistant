# CAD Work Assistant Design System

## Direction

CAD Work Assistant is a professional desktop tool for AutoCAD-connected quantity workflows. The interface should feel calm, precise, dense, and fast. It should look closer to CAD, engineering, and inspector-based productivity software than a SaaS landing page.

## UI UX Pro Max Inputs

- Query used: `engineering desktop software CAD application data dense interface`
- Adopted recommendations: data-dense dashboard structure, compact tables, hover row highlighting, clear loading and feedback states, visible focus states, keyboard navigation, tabular numeric presentation.
- Rejected recommendations: neon cyan, interference purple, magenta CTA, marketing landing structure. Those conflict with the CAD Work Assistant requirement for a calm technical workspace.
- Milestone 3 (Area): no separate UI UX Pro Max / 21st.dev session was available in this environment. Per the documented fallback order (design system → UI UX Pro Max → existing Length UI → general CAD/engineering UX judgment), Area reused this record's findings plus the already-shipped Length panel as its direct reference rather than re-deriving them - see `pages/measurement-workspace.md` for the resulting shared structure and the reasoning for keeping exclusions in one summary sentence instead of a per-row status column.

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
| Text Primary | `#17212B` | Primary text |
| Text Secondary | `#526170` | Labels, hints, metadata |
| Accent | `#1D6F8F` | Primary actions and active indicators |
| Success | `#1E7A47` | Connected/completed states |
| Warning | `#946200` | Recoverable problems |
| Error | `#B42318` | Blocking failures |
| Selection | `#CFE7F0` | Selected table rows |
| Focus | `#0B83A5` | Keyboard focus |

## Typography

- Sans: Segoe UI for native Windows readability.
- Numeric: Cascadia Mono, Consolas for quantities, coordinates, lengths, areas, and IDs.
- Use small, compact headings inside panels. Avoid hero-scale type in the workspace.
- Numeric values should use stable widths wherever repeated measurement values are shown.

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

## Components

- Sidebar navigation: grouped, compact, keyboard reachable.
- Command palette: `Ctrl+K`, searchable commands, closes with `Esc`.
- Data table: virtualized, row hover, selected row, long drawing names handled by trimming.
- Property inspector: selection summary, layer, units, calculation mode, rounding, open drawings.
- Status bar: connection, current DWG, units, selection.
- Activity log: timestamped feedback, not generic spinners.

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

## Anti-Patterns

- Purple AI gradients.
- Neon glow.
- Glassmorphism.
- Oversized hero sections.
- Decorative 3D effects.
- Large marketing cards.
- Fade-in animation for every element.
- Mouse-only workflows.
