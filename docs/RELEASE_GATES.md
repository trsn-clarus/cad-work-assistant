# Release Gates

Milestone 14 status is tracked in `docs/MILESTONE_14_DISTRIBUTION_RC.md`.

## Development

- Build passes for `CADWorkAssistant.CI.slnf`.
- Automated tests pass.
- Runtime audit passes for publish output when packaging is touched.
- Source changes are documented in the relevant docs when behavior changes.

## RC / Public Beta

- Product version uses `0.x` and release channel is `RC` or `Beta`.
- No known P0/P1 desktop or release packaging bugs.
- Release build, full tests, manual PDF generation, runtime audit, installer build, installer smoke, distribution verification, and ZIP generation pass.
- Installer is per-user and does not require administrator rights.
- User data is preserved by uninstall/reinstall.
- Distribution folder contains only user-facing artifacts: setup exe, setup SHA256, manual PDF, release notes, README_FIRST, optional notices, and release manifest.
- Known limitations clearly state that real AutoCAD validation is pending where applicable.
- Unsigned installer status and possible SmartScreen warning are documented.

## 1.0 Stable

- RC gate passes.
- Milestone 8.5 real AutoCAD validation passes.
- Milestone 11B real AutoCAD plot validation passes.
- Milestone 12B real AutoCAD text/Undo/Redo validation passes.
- Real AutoCAD regression pass covers plugin autoload, connection, selection, isolation, layer restore, WBLOCK, plot, CTB/STB, text editing, and Undo/Redo.
- No P0/P1 bugs.
- Release regression pass completed on a clean Windows profile or VM.
- Code signing for desktop exe, AutoCAD dll, and installer is strongly recommended.
