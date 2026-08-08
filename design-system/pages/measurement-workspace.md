# Measurement Workspace Rules (Length, Area, Vertical Area, Parapet)

Length (Milestone 2), Area (Milestone 3), and Vertical Area/Parapet (Milestone 4) are the four tools
under the QUANTITY nav group and must read as the same product, not four independently designed
screens.

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

Vertical Area/Parapet don't have a `WorkflowState` enum at all - they're live calculators, not
select-then-show tools, so there's no discrete "success moment" separate from "currently showing a
valid result." `StatusBrush` there is resolved from a handful of independent bools
(`Source.IsBusy`/`Source.IsError`/result-not-null/`IsInvalidHeight`) using the same brush keys as the
table above, but there was genuinely nothing to gain from wrapping them in an enum - an early attempt
at one was removed mid-implementation once most of its values turned out unused.

## Vertical Area / Parapet (Milestone 4)

These two are *composite* measurement tools - they don't just show a CAD-measured value, they combine
it with a user-entered condition (height, face mode, top width) and compute a result live as the user
types. That changes the body of the panel but not its bones:

1. Header row - same as Length/Area (title, status dot + text, primary action button), except the
   primary action (`[CAD에서 기준선 선택]` / `[CAD에서 둘레 선택]`) is only shown when the user has
   the "CAD에서 새로 선택" source radio active - visible/hidden rather than always-present, since it
   isn't always the relevant action.
2. A **source panel** (new, not in Length/Area) instead of the excluded-summary banner: three radio
   options (CAD selection / reuse Length's last measurement / manual entry) with the resolved length
   shown inline. This exists because Vertical Area/Parapet's input isn't "what did AutoCAD just
   return" - it's "which of three ways did the base length come from," and that choice needs to stay
   visible, not just flash by.
3. An **input panel** (new) - height, face mode, top surface toggle. Replaces the result table, since
   there's nothing to select from AutoCAD directly here - just numbers and a mode.
4. Result panel - same total-with-actions shape as Length/Area's footer, plus a formula line above the
   total showing the actual multiplication (`255.941 m × 0.100 m`, or for Parapet the two-line 측면/
   상부 breakdown) so the number is never presented without its derivation.

**Did not extract shared header/footer controls.** Two implementations (Length, Area) sharing verbatim
markup was already borderline; four implementations exist now but Vertical Area/Parapet's bodies are
different enough (radio-based source selection, live-recalculating numeric inputs) that a shared
`MeasurementStatusHeader`/`MeasurementResultFooter` control would only cover the header/footer rows,
saving a small amount of XAML at the cost of an extra indirection layer to read through. Revisit if a
fifth tool makes the header/footer duplication clearly worse.

**Did extract shared view-model logic**: `LengthSourceSelector` (Desktop.ViewModels) is the one piece
Vertical Area and Parapet share verbatim - acquiring a base length via CAD selection, Length-tool
reuse, or manual entry is identical logic in both, and Parapet was built as the second consumer within
the same milestone, which is exactly the point this file's "not yet a clear enough pattern" caveat
(for the *view* layer) stops applying (for the *view-model* layer).

## Milestone 4.5 consistency pass

All four panels got the same three changes, applied identically rather than per-tool judgment calls:

1. **Button hierarchy** - the header's CAD-selection button is `PrimaryButton`; `산출내역 추가` moved
   from `PrimaryButton` to `SecondaryButton` (it's a completion action next to the real primary
   action, not a second primary); `값 복사` moved to `QuietButton`. See `MASTER.md` "Button hierarchy"
   for why each of these is now its own `ControlTemplate` instead of a shared one.
2. **Numeric value/unit split** - each panel's one grand total (`TotalDisplay`) gained a sibling
   value-only property (`TotalValueDisplay`) so the footer can show the value in `NumericText` and the
   unit in `NumericUnitText` as two `TextBlock`s instead of one combined string. `TotalDisplay` itself
   is kept (unchanged) because clipboard copy and `QuantityRecord.CalculationExpression` still need
   the combined "value + unit" string. The unit `TextBlock`'s `Visibility` is bound through
   `StringNotEmptyToVisibilityConverter` against the same value-only property, so there's never a
   floating "m²" with no number in front of it before a result exists.
3. **Inline message styling** - any banner that isn't the excluded-summary case (which already used
   `PanelBorder`) moved to the shared `InlineMessageBorder`/`InlineMessageText` styles. Parapet's
   "양면 계산은 동일한 기준 길이를 두 면에 적용합니다" notice is the second consumer of this pattern
   after Length/Area's excluded-summary banner.

### A repeat of the `LastResult`-shaped bug, in Parapet this time

Milestone 4 already hit one instance of "a computed bool/property never raises `PropertyChanged`, so
a binding elsewhere silently goes stale" (`LengthWorkflowViewModel.LastResult`). Milestone 4.5's
Simulation Mode re-verification found the same shape of bug again: `ParapetWorkflowViewModel.FaceMode`
raises `PropertyChanged` for itself via `SetProperty`, but never for `IsSingleFace`/`IsBothFaces` -
the two bool properties computed from it that the "한 면"/"양면" `RadioButton`s and the new inline
banner's `Visibility` actually bind to. The `RadioButton`s still looked correct after clicking "양면"
in a screenshot, because `RadioButton` `GroupName` handles mutual exclusion at the control level
independent of the ViewModel notifying anyone - but the banner, which only had the `PropertyChanged`
event to go on, never appeared. Fixed by raising `OnPropertyChanged(nameof(IsSingleFace))` and
`OnPropertyChanged(nameof(IsBothFaces))` inside the `FaceMode` setter. **Pattern to watch for**: any
computed bool/string property derived from another observable property needs its own explicit
`OnPropertyChanged` in that property's setter - `SetProperty` only notifies for the property it was
called on, never for anything computed from it. A `RadioButton` bound to such a property can look
correct in a screenshot purely from `GroupName` behavior even when the underlying notification is
missing, so this class of bug needs a binding that *isn't* part of the same control group (like this
banner) to actually surface it.
