# Drawing PDF Output Rules (Milestone 11)

The third item in the OUTPUT nav group, right below PDF (`pages/pdf-export.md`, Milestone 10). This
screen is functionally unrelated to the other two OUTPUT screens even though the label looks
similar: Excel and PDF both turn *already-saved QuantityRecord data* into a document with no live
AutoCAD interaction beyond the initial data pull. This screen drives the real AutoCAD Plot engine -
it needs a live connection for every action, not just at load time, and has states the other two
screens never need (loading, no-PDF-device, an interactive "pick a window" step before the primary
action is even enabled).

## Why this screen doesn't reuse the Excel/PDF card shape

`pages/pdf-export.md` documented that PDF intentionally copied Excel's "single form → one primary
action → native OS dialog" shape because the two are functionally the same kind of task. Drawing PDF
output is not: it needs a live capability query before any option can be shown as available, a
conditional interactive sub-step (window selection) that gates the primary button, and it has to
represent "not connected" and "connected but no usable device" as first-class states rather than an
inline error banner. Copying the two-block card here would mean stuffing state machine logic into a
layout that wasn't built for it. So this screen gets its own layout while still reusing every
existing visual token - no new component, no new color, no new spacing scale.

## States and layout

```
Header: title ("도면 PDF 출력") + one-line description

State: Disconnected (IsConnected == false)
  centered two-line message, same empty-state pattern as every other project/connection-scoped panel

State: Connected, loading capabilities
  centered "Plot 기능 정보를 불러오는 중..."

State: Connected, capability load failed
  centered bold message (DescribeError text)

State: Connected, no PDF-capable device found
  centered bold message + CaptionText hint ("DWG To PDF.pc3 같은 PDF 장치가 설치되어 있는지 확인해주세요")

State: Ready (capabilities loaded, at least one PDF device)
  Card (PanelBorder):
    출력 범위: RadioButton 현재 Layout (+ ComboBox of Layouts, enabled only when this radio is checked)
               RadioButton Model 영역 지정 (+ [영역 지정] SecondaryButton + summary CaptionText,
               button enabled only when this radio is checked)
    ─ divider ─
    용지: RadioButton A4 / A3 - each individually disabled if PlotPaperMatcher found no match
          for that size against the real device's media list
    방향: RadioButton 자동 / 세로 / 가로
    색상: RadioButton 컬러(기존 설정 유지) / 흑백 - 흑백 disabled unless PlotStyleResolver says available
  Success banner (same InlineMessageBorder pattern as Excel/PDF): filename + optional simulation
    warning line + [파일 열기] [폴더 열기]
  Error banner (InlineMessageBorder + BrushError text)
  [PDF로 저장] (PrimaryButton, label swaps to "출력 중..." while exporting, disabled until the
    scope's requirement is satisfied - CurrentLayout has none, Window needs a window picked first)
```

## Per-option disabling instead of hiding

Unlike Excel/PDF's checkboxes (always available, they just toggle what's *included*), this screen's
paper-size and color radios represent real device capability, not user preference. A radio that
isn't actually supported by the connected device's real capabilities is rendered disabled rather
than hidden - the user can see "A3 exists as an option, but not on this device" instead of silently
never learning it was a possibility. This mirrors the same judgment already made for Length/Area's
excluded-object-type banners (§18 in the relevant Core docs): show what was excluded and why, don't
just quietly drop it. All of these disabled/enabled computations come straight from `Core.Plot`'s
pure resolvers (`PlotPaperMatcher`, `PlotStyleResolver`) evaluated against the real
`GetPlotCapabilities` response - the ViewModel never hardcodes which sizes or styles exist.

## The interactive sub-step: window selection gates the primary action

When Model 영역 지정 (Window scope) is selected, `PDF로 저장` stays disabled until `영역 지정` has been
clicked and returned a window - this is a deliberate two-step commit, not a single "click save and
we'll ask AutoCAD for a window mid-flow" design. Two reasons: the native `SaveFileDialog` should not
open while AutoCAD is mid-prompt for `Editor.GetPoint`/`GetCorner` (two different Windows apps both
soliciting the user's next click, with unpredictable focus/window-order results), and the summary
text after picking a window ("18,000 × 12,500 (도면 단위)") gives the user a chance to confirm the
area before committing to a save dialog. `현재 Layout` scope has no such gate - the primary button is
enabled as soon as capabilities finish loading.

## Verified in Simulation Mode (2026-08-09)

Same FakeAutoCad.exe + Desktop.exe two-process setup Milestones 2-10 established. Confirmed by
screenshot at each step: Ready state renders with real capability-driven presets (A4 selected by
default, A3 available since the `PlotSuccess` scenario's device supports it); `영역 지정` performs a
real Named Pipe round trip and the summary text updates to the exact scenario window dimensions;
`PDF로 저장` opens a real native `SaveFileDialog` pre-filled with the exact
`PlotOutputFileNameService`-generated name; after accepting the dialog, a real file appears on disk
(confirmed via direct filesystem check, not just the UI's claim) containing FakeAutoCad's
placeholder disclaimer text; switching FakeAutoCad scenarios and reconnecting correctly re-renders
the no-PDF-device state, the current-Layout-scope state (disables the window button, updates the
Inspector), and the Busy/Failure error state (plain red message, no raw exception text); stopping
FakeAutoCad renders the Disconnected state. The installed Setup EXE (not just the dev build) was
also launched in Simulation Mode and the Plot screen confirmed reachable and functional before the
test install was removed again.

## Native SaveFileDialog automation note

The `SetForegroundWindow` + `keybd_event VK_RETURN` technique from `pages/pdf-export.md` needed one
correction here: when enumerating the app's top-level windows via `EnumWindows` to find the dialog's
own handle, the dialog is the *first* match in Z-order (topmost), not the last - `EnumWindows`
returns windows front-to-back. An earlier attempt indexed the last match instead and silently sent
the Enter key to the wrong window (the main app window, which just ignores it). Once corrected to
index `[0]`, the technique worked identically to Excel/PDF's.
