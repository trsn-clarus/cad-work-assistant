# Text Tools Rules (Milestone 12)

Fourth item in the CAD nav group (Alt+4), right below Drawing. Unlike Length/Area/Vertical Area/
Parapet (read-only measurement) and Drawing (view-only navigation), this screen writes to the DWG -
it is the first CAD-group screen that mutates the document. That single fact drives every layout
decision below: batch edits must show exactly what will change before it happens, and errors need to
be specific enough that a locked-layer failure doesn't read like a generic connection problem.

## Why one screen with a segmented toggle, not two pages

The master prompt's own mockup implied two modes (edit an existing selection, create something new)
that share almost nothing structurally - a DataGrid + batch-property panel vs. a single-object form.
Splitting them into two nav entries would have doubled the CAD group for a feature that's really one
verb ("work with text") with two starting points. A compact `편집`/`작성` `RadioButton` pair at the
top of the panel switches between an outer edit `Grid` and an outer create `Grid`, each with its own
`Visibility` binding against the *panel's* DataContext (`TextWorkflowViewModel`) - see the
DataContext pitfall below for why that distinction matters.

## Layout

```
Header: title ("Text") + one-line status (last action result)
편집 ○  ● 작성   (RadioButton segmented toggle)

State: Disconnected
  same empty-state pattern as every other CAD/Quantity panel

State: Edit mode
  [CAD에서 문자 선택] (PrimaryButton)
  DataGrid: TYPE | CONTENT | LAYER | HEIGHT | COLOR  (click a row → app-wide Property Inspector shows full detail)
  Right panel:
    선택 요약 caption ("N개 문자 선택됨")
    내용: TextBox, enabled only when exactly one row is selected (§19 - batch content overwrite is disallowed)
    ☐ 높이 변경   [TextBox]     현재: {BatchPropertyState summary or "혼합"}
    ☐ 색상 변경   [ComboBox]    현재: {...}
    ☐ Layer 변경  [ComboBox]    현재: {...}
    (each input's IsEnabled is bound to its own checkbox - unchecked means untouched, not "no-op with the same value")
    Success/Error InlineMessageBorder
    [문자 변경 적용] (PrimaryButton, label swaps to "변경 적용 중..." while busy)

State: Create mode
  형식: ○ 단일행 문자  ○ 여러행 문자
  내용: multi-line TextBox
  높이: TextBox
  Layer: ComboBox (same drawing's layer list, defaults to current layer)
  색상: ComboBox (ByLayer/ByBlock + compact ACI palette, defaults to ByLayer)
  Success/Error InlineMessageBorder
  [CAD에서 위치 지정 후 작성] (PrimaryButton, label cycles "위치 지정 중..." → "작성 중...")
```

## Checkboxes gate batch edits, not free-form dropdowns

Every batch property (height/color/layer) is a checkbox + input pair, not a dropdown defaulting to
"no change." A dropdown's "no change" option looks identical in weight to every real choice, which
invites a slip where a user picks a specific layer thinking they're just looking at the list and
silently reassigns every selected object's layer. A checkbox that must be explicitly ticked before
its paired input becomes interactive makes "I am about to change this" a distinct, deliberate action -
the same reasoning `TextUpdatePatch`'s `OptionalValue<T>` applies at the data layer, carried through
to the input layer.

## Content editing is gated by selection count, not hidden

When more than one row is selected, the Content textbox is disabled rather than removed - the user
can still see what a representative item's content looks like (or a blank/mixed indicator), but
cannot type into it. Removing the field entirely would make it look like content editing doesn't
exist at all; disabling it communicates "this is possible, just not right now, because you have N
objects selected" - the same exclude-but-show judgment already used for unsupported object types in
Length/Area.

## No bespoke embedded inspector - the global Property Inspector does this job

The master prompt's ASCII mockup showed an inline detail block next to the table. Rather than build a
second, DataGrid-scoped detail panel duplicating what the app already has, clicking a row updates the
existing app-wide right-sidebar Property Inspector (the same one every other CAD/Quantity screen
populates) with that row's full detail (형식/내용/높이/Layer/색상/TextStyle/Handle), or a batch
summary when no single row is selected. One inspector pattern for the whole app, not screen-specific
variants.

## Native AutoCAD acquisition never opens a second dialog mid-flow

Both the selection button and "CAD에서 위치 지정 후 작성" hand control to AutoCAD's own interactive
prompt (`Editor.GetSelection`/`Editor.GetPoint`) rather than showing an in-app modal first - the same
principle Length/Area/Plot already established: this app never re-implements AutoCAD's own picking UX
with a substitute dialog. Create's two-step flow (acquire point, then create) is exposed as a single
button with a label that cycles through both sub-states rather than two separate buttons, so the user
doesn't have to understand the two-IPC-calls implementation detail to use it.

## A `Visibility` + `DataContext` pitfall worth remembering

Do not put a `Visibility` binding and a `DataContext` override on the same element. Setting
`DataContext` re-resolves every one of that element's *own* bindings - including sibling attributes
like `Visibility` - against the new context. The create panel's outer `Grid` binds `Visibility`
against the panel's own `TextWorkflowViewModel` DataContext; only the *inner* `StackPanel` overrides
`DataContext` to the composed `Create` (`TextCreateViewModel`). Doing it on one element silently
breaks `Visibility` (falls back to the CLR default, `Visible`) with no compile-time or binding-error
signal - it only shows up as two panels rendered on top of each other in an actual screenshot.

## Custom ComboBox templates need an explicit ItemTemplate for the closed-box text

`Themes/DesignTokens.xaml`'s `ComboBox` style renders the collapsed selection via
`SelectionBoxItemTemplate` in its `ControlTemplate`. For plain string-bound ComboBoxes this is
invisible, but the moment an item type needs `DisplayMemberPath` (as `CadColorDto` does), the closed
box shows the object's raw `ToString()` instead - `SelectionBoxItemTemplate` isn't auto-synthesized
from `DisplayMemberPath` alone the way the built-in ComboBox template does it. The fix is an explicit
`<ComboBox.ItemTemplate><DataTemplate><TextBlock Text="{Binding DisplayName}"/></DataTemplate></ComboBox.ItemTemplate>`
on the ComboBoxes that need it, not a change to the shared style (which would risk changing every
other ComboBox in the app that currently works fine without one).

## Verified in Simulation Mode (2026-08-09)

Same FakeAutoCad.exe + Desktop.exe two-process setup as every prior milestone. Confirmed by
screenshot at each step: real `SelectTextObjects` round trip renders mixed DBText/MText rows
correctly; batch height checkbox + apply performs a real `UpdateTextObjects` round trip and updates
the table in place; a locked-layer batch attempt fails atomically (table values unchanged) with the
specific Korean message naming the locked layer, not a generic connection-check message (the bug that
led to the `InvalidRequest`/`ApiExecutionFailed` reclassification documented in `docs/TEXT_TOOLS.md`
§8); killing FakeAutoCad mid-session disables the action buttons and shows "AutoCAD Not Running"
while preserving already-loaded rows; Create mode renders cleanly post-fix (no more edit-panel
bleed-through) and a real acquire-point-then-create round trip completes with a "문자 작성 완료"
status and a cleared content field. The installed Setup EXE was also smoke-tested (silent
install → launch in Simulation Mode → uninstall → user DB retained), not just the dev build.
