# Drawing Workspace Rules (Milestone 5)

Drawing is different in character from the Length/Area/Vertical Area/Parapet measurement panels
(`measurement-workspace.md`): those are "control → single result" tools, Drawing is "control +
object list + Layer list + Inspector" - closer to a file browser / property grid than a calculator.
It still runs on the same Milestone 4.5 Production Design System tokens - no new colors, no new
radius scale, no new button hierarchy invented for this page.

## UI UX Pro Max Inputs

- Queries used: `CAD layer manager`, `dense property panel`, `desktop selection workflow`,
  `engineering toolbar`.
- 21st MCP was not available in this environment (confirmed via `ToolSearch`, same as Milestone 4.5) -
  the documented fallback order applied: existing design-system → UI UX Pro Max → current UI →
  Windows conventions → AutoCAD/engineering patterns.
- Adopted: dense two-pane layout (selection table + property list side by side), checkbox-driven
  visibility toggles in a layer list, compact toolbar with a single primary action.
- Rejected: tree-view Layer managers (AutoCAD's own Layer Manager uses a flat sortable table, not a
  tree - matching that convention keeps the mental model familiar to the target user) and card-based
  object summaries (a dense `DataGrid` reads faster for repeated scanning, matching the density
  principle already established for measurement tables).

## Layout

Single workspace, not split across separate Selection/Layers/Export pages (§18) - the sidebar
originally reserved "Selection"/"Layers"/"Export" as separate disabled nav placeholders in Milestone
4.5's audit, but those were removed once Drawing shipped as one unified page covering all three
(`MainWindowViewModel.Navigation` comment documents this consolidation).

```
Header: title + status dot/text | Window/Crossing radio | [영역 선택] Primary
Secondary toolbar: [전체 보기] [선택 영역 보기] [선택 객체만 보기] [선택 Layer만 보기] [전체 복원]
Isolation banner (only when active)
Body: Selection table (DataGrid, *)  |  GridSplitter  |  Layer Manager (search + DataGrid, 320px)
Footer: Export (description → filename preview, [DWG로 저장])
```

## Toolbar button hierarchy (§86)

- **Primary**: `영역 선택` - the one action that starts the workflow (matches every measurement
  panel's single-Primary-button header).
- **Secondary**: `전체 보기`, `선택 영역 보기`, `선택 객체만 보기`, `선택 Layer만 보기` - all
  confirmatory/navigational actions that don't compete with the primary action for attention.
- **Quiet**: none in the toolbar itself; `전체 복원` uses Secondary rather than Quiet despite being a
  "recovery" action, because §94 explicitly calls for it to stay visually noticeable (a fully quiet
  Restore button would undercut the "don't let the user forget they're in an isolated view" goal).

## Selection table vs. Layer Manager

Both are `DataGrid`s inside the same density tokens as every other table in the app (32px header,
34px row - `DesignTokens.xaml`). The Layer Manager's visibility checkbox column is genuinely
interactive (`DataGridCheckBoxColumn` bound `TwoWay` to `LayerRow.IsOn`); Freeze/Lock columns are
read-only indicators (`DataGridCheckBoxColumn` bound `Mode=OneWay` - binding a read-only CLR property
without an explicit `OneWay` mode throws at runtime, see `docs/DRAWING_NAVIGATION.md` "실제로 겪은
버그" for how this was found and fixed).

## Layer search is real (§89-91)

Milestone 4.5 removed a Dashboard "Filter results" textbox that looked functional but filtered
nothing. The Layer Manager's search box is the first new filter control added since - it filters
`LayerWorkflowViewModel.FilteredLayers` by case-insensitive substring match on the Layer name,
computed in the ViewModel (`ApplyFilter()`), never in XAML or code-behind. Confirmed via Simulation
Mode: typing "wall" narrows a 5-layer list down to exactly `A-WALL`.

## Isolation state must stay visible (§94-96)

An `InlineMessageBorder` banner appears only while `IsIsolationActive` is true, right below the
toolbar - reusing the same inline-message pattern as Length/Area's excluded-summary banner and
Parapet's face-mode notice (`measurement-workspace.md`), extending that pattern to a third, non-tool
use: "you changed something temporary, here's how to undo it." The banner and the Property
Inspector's "격리 상태" row are two independent surfaces reporting the same
`DrawingWorkflowViewModel.IsIsolationActive` boolean - deliberately redundant, since forgetting an
active isolation and mis-reading a subsequent screenshot/selection is a worse failure mode than a
little repetition.

## Export footer

Filename suggestion updates live as the user types a description (`ExportWorkflowViewModel.
SuggestedFileName`, recomputed on every `Description` change) - confirmed in Simulation Mode typing
"실내마감표" against `School_Roof.dwg` produces `School_Roof_실내마감표.dwg` inline before the
`SaveFileDialog` even opens, matching the master prompt's own worked example (§53, §100). The actual
save path/overwrite confirmation is native `SaveFileDialog` (§57) - no custom file browser.

## Property Inspector

Drawing gets its own `RefreshInspector()` case in `MainWindowViewModel`, same live-binding pattern as
the four measurement tools (Milestone 4.5 §9): 상태/도면 개요/선택 객체/격리 상태, sourced directly
from `DrawingWorkflowViewModel`'s real state - no placeholder rows.
