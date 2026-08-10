# Project Workspace Rules (Milestone 13 Part A)

Not to be confused with `project-management.md` (Milestone 6's `ProjectDialog` - create/switch,
a modal). This is the new PROJECT-group "Projects" screen (Alt+2) that lets a user browse the
*entire* project list (beyond the 20-item recent cap `ProjectDialog` uses) and see one project's
drawing files, output history, activity, and quantity status without switching to it. Its purpose
is not to "prettily list" project data - it is to let a user find past work, understand DWG/output
status, and resume work immediately (master prompt §160).

## Layout

Reuses `HistoryPanel`'s (Milestone 7) two-pane skeleton verbatim rather than a Card Grid (explicitly
rejected for this screen by the master prompt, §47) - a dense engineering list, not a SaaS project
picker:

```
Header: title + count ("31개 프로젝트")                              [새 프로젝트]
Search box (*) | sort dropdown (최근 열기 / 프로젝트명 / 생성일, default LastOpenedAt desc)
Body: project list (*, GridSplitter divides it from the detail pane)
  Left:  DataGrid (● current-project glyph / 프로젝트 / 발주처 / 현장 / 도면 count / 최근 작업)
  Right: ScrollViewer detail pane (PROJECT / DRAWINGS / QUANTITY / OUTPUTS / ACTIVITY sections,
         each an `InspectorLabel`/`SectionTitle` block like every other detail pane in this app)
```

## No new visual patterns

Every token already existed: `PanelBorder`, `PrimaryButton`/`SecondaryButton`/`QuietButton`,
`SectionTitle`/`CaptionText`/`InspectorLabel`/`InspectorValue`, `InlineMessageBorder`/
`InlineMessageText`, the implicit `TextBox`/`ComboBox` styles, and `GridSplitter`. Current-project
state uses a glyph (`●`) next to the name, not color alone (§36, the same rule Connection status
and History's severity glyphs already follow) - `ProjectRow.CurrentGlyph`.

## Real bug: `Count` bound straight into `BooleanToVisibilityConverter`

Caught during code review before any Simulation Mode screenshot was taken, not from a rendering
bug - but it is the exact same class of defect `HistoryPanel` itself already documented once
(`docs/QUANTITY_VERIFICATION.md` §9): `Visibility="{Binding Rows.Count, Converter=
{StaticResource BoolToVisibility}}"` binds an `int` into a converter whose pattern match is
`value is bool`, which never matches, so the empty-state layout would have been wrong for every
list on this screen (Drawings/Outputs/Activity, not just the project list) regardless of actual
count. Fixed by adding real `bool` properties (`HasRows`/`HasDrawingFiles`/`HasOutputs`/
`HasRecentActivity`) to `ProjectsWorkspaceViewModel`, raised manually at every point the underlying
collection is mutated, and binding those instead - worth checking for on every future
`ObservableCollection`-backed empty state on this app's dense-list screens.

## Real bug: `DrawingFile` auto-registration race across two call sites

Fixing one real gap (a drawing already open in AutoCAD before its project existed, or before the
user switched to it, never got auto-registered - `MainWindowViewModel.TryRegisterActiveDrawing()`'s
session-scoped "already seen this path" guard prevented it) introduced a second, subtler bug on the
very next Simulation Mode screenshot: the fix added a second caller of the Projects detail-loading
path (`ProjectsWorkspaceViewModel.RefreshCurrentDetailAsync()`), which could race with the existing
`CurrentProjectChanged`-triggered refresh, and both independently appended the same drawing to the
same `ObservableCollection`, producing a visible duplicate row (while the separately-sourced
Inspector count correctly still said "1개" - the two data paths disagreeing was itself the tell).
Fixed with a monotonic `_detailLoadVersion` counter checked after every `await` inside
`LoadDetailAsync()` before mutating shared state - the same "stale response" shape
`TextWorkflowViewModel.LoadDetailAsync` (Milestone 12) already guards with a simpler
selection-only check, generalized here to also cover same-selection-but-newer-call races. See
`docs/ARCHITECTURE.md` §8.12 and `docs/PERSISTENCE.md` §11 for the data-layer side of this fix.

## Real bug (found on a different screen, same root cause as Milestone 12's color picker)

`DrawingPdfExportPanel.xaml`'s Layout-selection `ComboBox` used `DisplayMemberPath="Name"` only,
which - for this app's custom `ControlTemplate` - does not populate `SelectionBoxItemTemplate` (the
closed/collapsed box's own display), so the disabled Layout selector showed the raw
`CADWorkAssistant.Core.Plot.CadPlotLayoutDto` type name instead of the layout's name. This is the
exact same defect Milestone 12 already found and fixed once in the Text Tools color picker -
finding it again independently on an unrelated screen confirms it is a property of this app's
shared `ComboBox` template, not a one-off mistake. Fixed identically (explicit `ComboBox.ItemTemplate`
replacing `DisplayMemberPath`); confirmed via `Grep` that no other `DisplayMemberPath`-only
`ComboBox` remains anywhere in `src/CADWorkAssistant.Desktop/Views/`.
