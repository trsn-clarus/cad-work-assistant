# Workspace Page Rules

The Dashboard (`SelectedTool == "Dashboard"`, `IsDashboardContentVisible`) is the landing screen when
no QUANTITY tool is active. As of Milestone 4.5 it is a real control center, not a marketing home -
every number on it comes from something the user actually did this session.

- No metric cards. An earlier version showed four `UniformGrid` cards ("Selected objects", "Total
  length", "Area queue", "Last export") seeded with hardcoded sample values at startup - permanently
  fake, since nothing in the app ever updated them. Removed outright rather than wired up, because
  nothing real backs a per-session "Area queue" or "Last export" concept yet; add it back only when a
  real feature produces that data.
- `Quantity Results` and `Activity Log` are the two real panels. Both start empty every session and
  fill only from genuine events: `Quantity Results` from any of the four measurement tools'
  `RecordAdded`, `Activity Log` from the same event (one line per record: type, layer, object count,
  value). Never seed either with sample rows - a fake row sitting among real measurements is
  indistinguishable from a real one and will get trusted as data.
- Empty state is action-oriented text, not an illustration: "아직 추가된 산출내역이 없습니다" +
  "Length / Area / Vertical Area / Parapet에서 측정 후 "산출내역 추가"를 누르면 여기에 표시됩니다."
  Tells the user exactly what to do next, in the same panel where the result will appear.
- The header's only action button is the Inspector toggle. A generic "Extract Length" button and a
  generic "Copy" button used to sit here, both disconnected from any specific tool's result (there
  are four tools now - which one would "Extract Length" run?) - removed along with their dead
  `RunExtractionCommand`/dashboard-level `CopyResultCommand`. Each measurement panel already has its
  own real Copy/Add-to-Quantity buttons scoped to its own result.
- No text filter box unless it actually filters something. The Dashboard used to show a "Filter
  results" `TextBox` bound to the same `CommandQuery` property as the Command Palette search - it
  didn't filter the `QuantityRecords` grid at all, and typing in it would also silently affect
  Command Palette state. Removed; add a real filter only when there's an actual filtering
  implementation behind it.
- Preserve long drawing filenames with trimming rather than breaking the layout.
- Put recovery hints in activity or error states, not in decorative empty-state illustrations.
