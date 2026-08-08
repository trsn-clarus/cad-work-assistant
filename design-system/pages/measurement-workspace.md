# Measurement Workspace Rules (Length, Area)

Length (Milestone 2) and Area (Milestone 3) are the first two tools under the QUANTITY nav group and
must read as the same product, not two independently designed screens. Vertical Area/Parapet
(Milestone 4+) should follow the same shape.

## Shared structure

Every measurement panel (`LengthPanel`, `AreaPanel`) uses the same four-row layout inside its own
`Grid`, swapped in for the dashboard content based on which QUANTITY nav item is selected:

1. Header row - tool title (`FontSize=22 SemiBold`), a status dot (`StatusBrush`) + `StatusText`
   caption underneath, and a single primary action button on the right (`[CAD에서 객체 선택]` /
   `[CAD에서 영역 선택]`).
2. Excluded-summary banner - a `PanelBorder` shown only when there is something to explain
   (`HasExcludedSummary`). Never a red/error style - exclusions are expected, routine information.
3. Result table - `PanelBorder` containing a `SectionTitle` ("선택 객체" / "선택 영역") and a
   `DataGrid` of HANDLE / TYPE / LAYER / VALUE (right-aligned, `FontMono`). Only successfully
   computed rows appear here; anything excluded is summarized in the banner above instead of shown
   as a disabled row, so the table never mixes "answer" rows with "why not" rows.
4. Total footer - `PanelBorder` with the total value in `NumericText` at `FontSize=24`, plus
   `[값 복사]` and `[산출내역 추가]` buttons.

## Why one table, one banner (not a per-row status column)

Area has more failure modes than Length (open/unsupported/invalid geometry vs. just
unsupported-type). It would be possible to show every selected object - valid or not - in one table
with a State column. We deliberately did not: the primary number the user came for (§106, "3,102.43
m²") and the objects that produced it should be the only thing in the table. Exclusions are
summarized as one sentence ("선택한 4개 객체 중 1개는 면적 계산에서 제외했습니다 (열림 1개)"),
grouped by reason, instead of one row per reason. This keeps the table exactly as dense for Area as
it is for Length.

## State-to-brush mapping

Both view models expose `StatusBrush`, resolved from the same design tokens:

| Workflow state | Brush |
| --- | --- |
| Success | `BrushSuccess` |
| PartialSuccess (Area only) | `BrushWarning` |
| AwaitingSelection | `BrushWarning` |
| Error | `BrushError` |
| Idle / Cancelled / EmptySelection / NoValidObjects | `BrushTextMuted` |

Cancel and "selected but nothing usable" are not error states - color communicates this, but
`StatusText` always carries the same information in words (never color-only, per accessibility
rules in `MASTER.md`).

## Extending to Vertical Area / Parapet

New measurement tools should reuse this same four-row shape rather than inventing a new one. If a
third tool needs the same header/footer markup verbatim, extract `MeasurementStatusHeader` /
`MeasurementResultFooter` at that point - not before, since two implementations (Length, Area) are
not yet a clear enough pattern to abstract confidently.
