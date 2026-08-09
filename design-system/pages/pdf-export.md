# PDF Export Rules (Milestone 10)

The second item in the OUTPUT nav group (`pages/excel-export.md` was the first, Milestone 9). Same
interaction shape as Excel's screen - "single form → one primary action → native OS dialog" - and
deliberately kept that way: PDF and Excel export are functionally the same kind of task from the
user's point of view (pick a scope, pick what to include, generate a file), so they get the same
screen shape rather than a second bespoke layout.

## No new visual patterns, and no new component even though it's a second screen

Every token is the same set Excel already established: `PanelBorder`, `PrimaryButton`/
`SecondaryButton`/`QuietButton`, `SectionTitle`/`CaptionText`, `InlineMessageBorder`/
`InlineMessageText`, the implicit `RadioButton`/`CheckBox` styles, `BrushDivider`. `PdfExportPanel.xaml`
is structurally a near-duplicate of `ExcelExportPanel.xaml` (same three-block card, same
Success/Error banner placement, same empty-state block) rather than a shared generic
"OutputExportPanel" control. This was a deliberate choice, not missed reuse: the two screens' option
sets differ in real, non-cosmetic ways (Excel's four checkboxes map to sheet on/off, PDF's map to
fields inside a detail block; Excel's summary counts a spreadsheet, PDF's counts a document), and a
generic control would need conditional branches for those differences anyway. Two small, obviously
parallel files are easier to read than one control with an option-dependent behavior branch inside
it - the same judgment call this codebase already made for `ExcelExportOptions`/`PdfExportOptions`
staying separate classes (`docs/PDF_EXPORT.md` §4) rather than one shared options type.

## Layout

```
Header: title ("PDF 산출근거서") + current project name (CaptionText)
Body (max-width 520, left-aligned):
  Card: 내보내기 범위 (2 RadioButtons: 전체 수량 / 검토 완료 수량만)
        ─ divider ─
        포함 정보 (4 CheckBoxes: 산출근거/검산결과/검토메모/원본 DWG 파일명)
        ─ divider ─
        live summary text (CaptionText, "총 N건 · 검토 완료 X · 확인 필요 Y · 검산 오류 Z")
  Success banner (InlineMessageBorder): filename + [파일 열기] [폴더 열기]
  Error banner (InlineMessageBorder + BrushError text)
  [PDF 보고서 생성] (PrimaryButton, label swaps to "생성 중..." while exporting)
Empty state (no current project): centered two-line message, same pattern as every other
  project-scoped panel
```

## The native SaveFileDialog, and the lesson Milestone 9 already paid for

Milestone 9 documented in detail (`pages/excel-export.md`) that this app's native `SaveFileDialog`
is hard to automate reliably in Simulation Mode, and specifically that synthetic mouse clicks on it
once caused the whole app to hang. That lesson was applied here from the start rather than
rediscovered: PDF export verification went straight to focusing the dialog's own window handle
(found via `EnumWindows` + `GetWindowThreadProcessId`, since it's a second top-level window sharing
the app's PID) and sending a plain `keybd_event` `VK_RETURN` to accept the dialog's own pre-filled
suggested filename. This worked cleanly on the first attempt, both against the dev build and the
installed EXE - no repeat of the earlier hang.

## Real bug found during Simulation Mode testing: mismatched `AutomationProperties.Name`

The primary button was written with `AutomationProperties.Name="PDF 파일 생성"` while its `Content`
binds to `PdfExportViewModel.ExportButtonLabel`, whose default-state value is `"PDF 보고서 생성"` -
two different Korean phrases for the same button. UI Automation search by the visible label therefore
found the button's child `TextBlock` (whose own implicit accessible name comes from its own text)
instead of the button itself, and `InvokePattern` failed with "unsupported pattern" - the same class
of bug `docs/QUANTITY_VERIFICATION.md` documented for a `HistoryPanel` button, and conceptually the
same root cause CLAUDE.md's coding-conventions section already warns about (`Button` without an
explicit `AutomationProperties.Name` lets UI Automation resolve to a child `TextBlock` first). Here
the button *did* have an explicit name - it just didn't match the real label. This is a genuine
accessibility defect independent of testing: a screen reader user would hear "PDF 파일 생성" while
sighted users read "PDF 보고서 생성" on screen. Fixed by setting `AutomationProperties.Name="PDF 보고서
생성"` to match - the same value Excel's equivalent button already used, since Excel's static
`AutomationProperties.Name` happened to equal its own default-state label from the start.

## Cover/summary vs. per-item detail: reused from `docs/PDF_EXPORT.md`, not re-derived here

The actual PDF *document's* structure (cover page, summary table, per-item detail blocks combining
calculation/verification/review into one block instead of three separate sections) is a content and
information-architecture decision, not a WPF visual-design one - it's documented in
`docs/PDF_EXPORT.md` §5 rather than duplicated here, the same split Milestone 9 used between this
directory (screen chrome) and `docs/EXCEL_EXPORT.md` (workbook content).
