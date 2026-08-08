# Project Management Rules (Milestone 6)

Unlike Drawing (`drawing-workspace.md`, "control + list + inspector") or the measurement panels
(`measurement-workspace.md`, "control → single result"), Project Management is a single modal
dialog plus one sidebar entry point - it does not get its own Navigation item or full-page
workspace, matching §18's "don't split into too many pages" principle applied consistently since
Milestone 4.5.

## Layout

```
Sidebar (existing PROJECT group, row inserted above the Command button):
  [folder glyph] {CurrentProjectName}                         → opens ProjectDialog

ProjectDialog (Window, 480x620, CenterOwner, resizable):
  새 프로젝트 (PanelBorder)
    프로젝트 이름 * / 발주처 / 현장 / 설명 (TextBox, implicit style)
    [프로젝트 만들기] PrimaryButton, right-aligned
  InlineMessageBorder (status/error text, only visible when non-empty)
  최근 프로젝트 (SectionTitle)
    SecondaryButton per project (Name + Client · LastOpenedAt), or empty-state caption
```

## No new visual patterns

Every token used already existed before this milestone: `PanelBorder`, `PrimaryButton`/
`SecondaryButton`, `CaptionText`/`SectionTitle`, `InlineMessageBorder`/`InlineMessageText`
(Milestone 4.5's warning/notice banner pattern, reused here for create/open status and errors -
the same pattern Parapet's face-mode notice and Drawing's isolation banner already use), and the
implicit `TextBox` style. No new colors, no new button tier, no new radius scale. The dialog is a
second top-level `Window` (like `MainWindow`) rather than a `UserControl` hosted inside
`MainWindow`, because it needs to be modal (`ShowDialog`) and block interaction with the
measurement tools while a project decision is pending - the codebase had no precedent for a modal
dialog before this milestone, so this is the one genuinely new *structural* piece, not a new
*visual* one.

## Quick session vs. an open project

The sidebar button's label doubles as the empty state: `"빠른 세션 (프로젝트 없음)"` when no
project is open, or the project's name once one is. There is no separate "no project" banner or
icon state - the same button that opens the switcher also communicates current status, avoiding a
second UI surface for the same fact (the same reasoning Drawing's isolation banner + Inspector row
redundancy explicitly rejected for a *different* reason - here the goal is fewer surfaces, there it
was deliberate redundancy for a state easy to forget while working).

## Real bug found via UI Automation click testing

The "최근 프로젝트" list button and the sidebar project-switcher button did not initially carry an
explicit `AutomationProperties.Name` on the `Button` element itself - only on inner `TextBox`/
`TextBlock` children. WPF's default automation-name computation for a `TextBlock` returns its own
`Text`, so a name-based UI Automation search (`FindFirst` by `NameProperty`) matched the *label*
TextBlock instead of the enclosing `Button`, and invoking it threw "Unsupported Pattern" since a
TextBlock has no `InvokePattern`. Fixed by giving every clickable `Button` and every `TextBox` an
explicit, non-colliding `AutomationProperties.Name` (`"프로젝트 열기: {ProjectName}"`,
`"발주처 입력"` instead of `"발주처"`, etc.) - documented in `docs/PERSISTENCE.md` §9 and
`CLAUDE.md`'s coding-convention "실제로 겪은 예" list as a reusable lesson for any future dialog
with label+input pairs or button content built from multiple text elements.

## Auto-save, no Save button

Consistent with the master prompt's explicit requirement (§158): there is no "Save Project"
button anywhere in this UI. Creating a project persists immediately; opening one loads its data
immediately; every subsequent action (adding a quantity record, completing an export) commits on
its own. The dialog's only buttons are the ones that change *which* project is active, never one
that persists in-place state.
