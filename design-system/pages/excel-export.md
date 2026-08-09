# Excel Export Rules (Milestone 9)

The first real screen in the OUTPUT nav group (`pages/quantity-history.md` retired the placeholder
concept in Milestone 8 - see `MASTER.md` "Navigation"). It is a fourth interaction shape after
measurement panel / Drawing / History: "single form → one primary action → native OS dialog", closer
to a settings form than to any of the other three.

## No new visual patterns

Every token is one already established: `PanelBorder`, `PrimaryButton`/`SecondaryButton`/
`QuietButton`, `SectionTitle`/`CaptionText`, `InlineMessageBorder`/`InlineMessageText` (reused a
fourth time here, after Length/Area's excluded-summary, Drawing's isolation banner, and History's
Compare banner), the implicit `RadioButton`/`CheckBox` styles, and `BrushDivider` for the two
section separators inside the form card. No new component was built for the scope picker or the
include-toggles - a `RadioButton` group and four `CheckBox`es are the correct native controls for
"choose one of two" and "toggle several independent options," so nothing here needed a custom
control.

## Layout

```
Header: title ("Excel 수량산출서") + current project name (CaptionText)
Body (max-width 520, left-aligned - a form, not a wide table):
  Card: 내보내기 범위 (2 RadioButtons)
        ─ divider ─
        포함 정보 (4 CheckBoxes)
        ─ divider ─
        live summary text (CaptionText, "총 N건 · 검토 완료 X · 확인 필요 Y · 검산 오류 Z")
  Success banner (InlineMessageBorder, only after a successful export): filename + [파일 열기] [폴더 열기]
  Error banner (InlineMessageBorder + BrushError text, only after a failed export)
  [Excel 파일 생성] (PrimaryButton, label swaps to "생성 중..." while exporting)
Empty state (no current project): centered two-line message, same pattern as every other
  project-scoped panel (Length/Area/Vertical Area/Parapet/History all show an equivalent message)
```

The two banners and the button below them are structurally three separate optional blocks, not one
state machine rendered as a single region - `IsSuccess`/`IsError` are mutually exclusive
(`ExcelExportViewModel.ExportAsync` always resets both to `false` before a new attempt), but the XAML
does not encode that exclusivity itself, matching how every other workflow panel in this app
(Length/Area/Vertical Area/Parapet) already renders its own success/error state.

## Scope and toggles are plain settings, not a wizard

`내보내기 범위`(전체 수량 / 검토 완료 수량만) and the four `포함 정보` checkboxes are all visible at
once, with no "next step" gating - the user can change any of them and immediately see the live
summary text update (`ExcelExportViewModel` recomputes `SummaryText` on every relevant property
change, without a separate "preview" button). This mirrors Vertical Area/Parapet's live-recalculate-
on-input pattern (Milestone 4) rather than introducing a multi-step wizard, which the master prompt
never asked for and which would be slower for a form this small.

## The native `SaveFileDialog`, not an in-app one

Clicking `Excel 파일 생성` opens `Microsoft.Win32.SaveFileDialog` - the first screen in this app to
hand control to a native OS dialog instead of an owned WPF `Window` (`ProjectDialog` is the
established in-app-dialog pattern used everywhere else). This was a deliberate choice, not an
oversight: the user is picking a real save location on their file system for a document they intend
to open in Excel or send to someone else, which is exactly what the native Save dialog is for -
building a custom in-app file browser would be strictly worse (no recent-locations list, no network-
path support, unfamiliar to users who already know the Windows dialog).

### Automating the native dialog in Simulation Mode was materially harder than anything else in this app

UI Automation reliably drives every other dialog in this codebase (`ProjectDialog`, nav buttons,
checkboxes) via `InvokePattern`/`ValuePattern` on WPF `AutomationElement`s. The Explorer-hosted
`SaveFileDialog` does not expose its Save/Cancel buttons the same way: they appear in the automation
tree as `ControlType.Pane` (not `ControlType.Button`) with `AutomationId="1"`/`"2"`, and
`GetSupportedPatterns()` on them returns nothing usable from managed `System.Windows.Automation` in
this environment - `InvokePattern` and `LegacyIAccessiblePattern` are both unavailable. Raw synthetic
mouse clicks (`SetCursorPos` + `mouse_event`) were tried as a fallback and once caused the entire
dialog (and the app's UI thread with it) to hang indefinitely with the window title suffixed
"(응답 없음)" (Not Responding) - confirmed genuinely stuck, not just slow, by polling
`Process.Responding` for several minutes and by two independent recovery attempts (synthetic
mouse-up, `Escape` keydown) both failing to un-stick it. No partial or temp `.xlsx` had been written
to disk at that point, which places the hang inside the native dialog's own message loop, before the
app's `ExportAsync` code path was ever reached - not a defect in this app's export code.

**What worked reliably**: after the dialog opens with its already-correct suggested filename
pre-filled (`ExcelExportViewModel.ExportAsync` builds it via `ExportFileNameService.Sanitize`), a
plain `keybd_event` `VK_RETURN` (Enter) sent to the dialog's own window handle (found via
`EnumWindows` + `GetWindowThreadProcessId`, not via the WPF automation tree, since the dialog is a
second top-level window sharing the app's PID) triggers its default button and completes the save
cleanly every time - both against the dev build and, separately, against the installed EXE. This is
now the documented approach for automating this specific screen in any future Simulation Mode
session: accept the dialog's own default filename/location and press Enter, rather than attempting
to redirect the save path or click the Save button with the mouse.

## Reused, not reinvented: file-name and export-history conventions

The suggested filename (`<프로젝트명>_수량산출서_<yyyyMMdd>.xlsx`) reuses
`Core.Drawing.ExportFileNameService.Sanitize` (Milestone 5's WBLOCK export sanitizer) rather than a
new one. The post-export history entry follows the exact `IsExporting`/`IsSuccess`/`IsError` bool-flag
shape `ExportWorkflowViewModel` (Milestone 5) already established, so a developer who has seen any
other export/workflow panel in this app already knows this screen's state machine.
