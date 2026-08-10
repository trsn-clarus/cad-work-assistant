# CAD Work Assistant 0.9.0 Release Candidate

CAD Work Assistant 0.9.0 RC is a distribution release candidate for public beta installation testing. It is intended for desktop, project, quantity history, Excel/PDF, manual, and installer validation outside the development environment.

## Major Capabilities

- AutoCAD connection architecture using a desktop app plus AutoCAD plugin over local Named Pipes.
- Length, area, vertical area, and parapet quantity workflows.
- Drawing workspace for selection, layer isolation, navigation, and extraction workflows.
- Project manager with project search, switching, drawing relink, output history, and activity history.
- Quantity history with verification and review states.
- Excel quantity export.
- PDF quantity report export.
- Drawing PDF / plot workflow architecture and simulation validation.
- Text tools workflow architecture and simulation validation.
- Per-user installer with AutoCAD ApplicationPlugins bundle.
- Installed Korean user manual PDF.

## Installation

Run `CADWorkAssistant-Setup-0.9.0-RC-x64.exe`.

The installer is per-user and does not require administrator rights. User data is stored under `%LOCALAPPDATA%\CADWorkAssistant\` and is preserved when the application is uninstalled.

## Known Limitations

- Real AutoCAD 2024 validation is still pending for plugin autoload, real Named Pipe connection, real length/area selection, visibility isolation, layer restore, WBLOCK, real plot output, CTB/STB behavior, text editing, and Undo/Redo.
- Drawing PDF and Text Tools are available for release-candidate workflow validation, but real AutoCAD output validation is still pending.
- This RC is not a 1.0 production release.
- The installer is unsigned. Windows SmartScreen may show a warning on first run.

## Recommended Beta Practice

This release is designed for AutoCAD 2024 workflows. Until real-machine validation is complete, use backup copies of important DWG files when testing CAD-modifying workflows.

## Offline And Privacy

- No internet connection is required.
- No AI, LLM, cloud sync, analytics, telemetry, login, license server, or auto-update service is included.
- No firewall rule is created.
- The application communicates locally with AutoCAD through Named Pipes.

## Support Information

When reporting an issue, include:

- CAD Work Assistant version: `0.9.0`
- Release channel: `Release Candidate`
- Windows version
- AutoCAD version
- Error message
- Workflow being performed
- Relevant log file from `%LOCALAPPDATA%\CADWorkAssistant\logs\`

Review logs before sharing them externally, because paths or drawing names may appear in diagnostic messages.

## 1.0 Production Gate

1.0 requires the RC gate plus real AutoCAD validation for plugin integration, drawing PDF output, and text tools, no P0/P1 release bugs, release regression pass, and preferably signed desktop/plugin/installer artifacts.
