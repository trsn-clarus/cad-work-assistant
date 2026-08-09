# Quantity History Rules (Milestone 7)

History is a third distinct interaction shape alongside the measurement panels
(`measurement-workspace.md`, "control → single result") and Drawing
(`drawing-workspace.md`, "control + object list + secondary pane"). History is "list + detail
inspector + batch actions" - closer to Drawing than to a measurement panel, so it reuses Drawing's
exact two-pane skeleton rather than inventing a fourth layout shape.

## No new visual patterns

Every token is one already established before this milestone: `PanelBorder`, `PrimaryButton`/
`SecondaryButton`/`QuietButton`, `SectionTitle`/`CaptionText`/`InspectorLabel`/`InspectorValue`/
`NumericText`/`NumericUnitText`, `InlineMessageBorder`/`InlineMessageText` (reused a third time here
for the Compare result banner, after Length/Area's excluded-summary and Drawing's isolation banner),
and the `GridSplitter` two-pane layout from `DrawingPanel`. No status-pill/badge component was
introduced - severity is communicated the same way connection state already is (Milestone 4.5 §67-68):
a distinct glyph plus text, never color alone.

## Layout

```
Header: title + one-line summary ("42건 · 검산 완료 35 · 확인 필요 5 · 오류 1 · 미검산 1")
        | [취소] (only while a batch is running) [선택 항목 검산] [전체 검산]
Filter toolbar: search box (*) | type filter | verification filter | review filter
Body: results table (*, GridSplitter divides it from a fixed-width detail pane, 340px)
  Left:  DataGrid (checkbox column, DATE/TYPE/DESCRIPTION/QTY/UNIT/VERIFICATION/REVIEW)
  Right: Inspector-style detail pane (RESULT / SOURCE / VERIFICATION / REVIEW sections)
Footer: Compare result banner (only when two rows are checked and compared) | [비교]
```

## Severity glyphs (§58, §113)

Row-level (`QuantityHistoryRow.VerificationGlyph`, 5 states): `✓` Pass / `!` Review / `×` Error /
`—` not yet checked / `?` checked but not machine-verifiable (Info). Per-check glyphs inside the
Inspector's VERIFICATION list (`VerificationSeverityToGlyphConverter`, 4 states since every check
that ran has *some* outcome): `✓`/`ⓘ`/`!`/`×`. Both pair the glyph with a paired-brush color
(`VerificationSeverityToBrushConverter`, reusing `BrushSuccess`/`BrushWarning`/`BrushError`/
`BrushTextMuted` - no new palette entries) and the glyph is never shown without adjacent text.

## Batch selection: a checkbox column, not multi-select

WPF `DataGrid` multi-select bound to a ViewModel collection is awkward without extra plumbing
(§116 anticipated this and explicitly allowed a checkbox-column fallback). `IsChecked` drives
"선택 항목 검산" and "비교"; row click/`SelectedItem` independently drives which record the
Inspector shows - checking a row for a batch action does not change what's in the detail pane, and
selecting a row for inspection does not check it. This mirrors a real, deliberate UX split
(inspect vs. bulk-act are different intents) rather than reusing one selection concept for both.

## Real bug: `DataGridCheckBoxColumn` binding never committed

The batch-select checkbox column was originally a `DataGridCheckBoxColumn` bound
`Mode=TwoWay` (the same syntax `DrawingPanel`'s `LayerRow.IsOn` column already used successfully in
Milestone 5). In this screen's particular `DataGrid` configuration, clicking the checkbox toggled
its visual state but never committed to the bound `QuantityHistoryRow.IsChecked` property - confirmed
by adding a temporary diagnostic log inside the property setter and observing it never fired, across
both UI Automation `TogglePattern` and synthetic real mouse clicks (with the window properly
foregrounded and the calling process DPI-aware). Replacing the column with a
`DataGridTemplateColumn` containing a plain `CheckBox` (`IsChecked="{Binding IsChecked, Mode=TwoWay,
UpdateSourceTrigger=PropertyChanged}"`) fixed it immediately, confirmed by the same diagnostic log
firing on the next click. A plain `CheckBox` inside a template column commits on its own `Click`
event and never depends on the `DataGrid`'s cell `BeginEdit`/`CommitEdit` lifecycle, which is the
likely reason it is more robust here. Documented in `docs/QUANTITY_VERIFICATION.md` §9 and
`docs/ARCHITECTURE.md` §11 as a decision-log entry, since this is a reusable lesson for any future
checkbox column on this screen's `DataGrid` configuration.

## Real bug: `TextBlock`-only style applied to a `Run`

The Inspector's RESULT section originally displayed the numeric value and unit as two `<Run>`
elements inside one `<TextBlock>`, each with `Style="{StaticResource NumericText}"` /
`NumericUnitText` - styles whose `TargetType` is `TextBlock`, not `Run`
(`System.Windows.Documents.TextElement`). This threw `XamlParseException` wrapping
`InvalidOperationException` during `MainWindow.InitializeComponent()`, crashing the app before any
window appeared - caught via the Serilog unhandled-exception log, not visually (there was nothing to
see). Every other panel in this app already applies `NumericText`/`NumericUnitText` to sibling
`TextBlock`s inside a horizontal `StackPanel`, never to `Run`s - History's Inspector now follows
that same established pattern instead of introducing a new one.

## Real bug: nullable reference bound directly to `InverseBooleanToVisibilityConverter`

The "아직 검산하지 않았습니다" empty-state message was bound
`Visibility="{Binding SelectedRow.Verification, Converter={StaticResource InverseBoolToVisibility}}"`
- passing a `QuantityVerificationResult?` (not a `bool`) into a converter whose pattern match is
`value is true`. A non-bool reference is never pattern-matched as `true`, so the converter always
returned `Visible` regardless of whether verification data existed, and the empty-state text
rendered permanently overlapping five real, populated check rows in a Simulation Mode screenshot.
Fixed by adding a real `bool HasVerification` property to `QuantityHistoryRow` and binding that
instead - the same class of fix `StringNotEmptyToVisibilityConverter` already existed to solve for
strings (Milestone 4.5 §16-17); nullable-reference-to-bool-converter mismatches are the general
form of that bug and worth checking for on every new empty-state binding.

## Milestone 8: column width squeeze

The results `DataGrid`'s DESCRIPTION column was originally a fixed `Width="150"`, and the seven
columns' fixed widths summed to more than the list pane actually has at typical window sizes. A
Simulation Mode screenshot showed the UNIT column's header rendered as just "UI" (WPF's normal
ellipsis truncation on a too-narrow column, not a crash). Fixed by making DESCRIPTION `Width="*"`
(so it absorbs whatever space is left instead of demanding a fixed 150px) and trimming the other six
columns' fixed widths so their sum comfortably fits the pane. Headers can still abbreviate under a
narrow pane (e.g. "U." for UNIT) - that is normal `DataGridColumn` header ellipsis, not a bug, and
the full value is always available in the Inspector detail pane on the right.
