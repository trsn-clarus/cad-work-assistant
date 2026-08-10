# Release Checklist

Use `PASS`, `FAIL`, `BLOCKED`, or `N/A` for every item before publishing.

## Repository

| Item | Status | Notes |
| --- | --- | --- |
| `git status` checked | PASS | `.claude/scheduled_tasks.lock` is development-only and not release content. |
| Tracked source clean before formal release | PASS | Formal RC build runs without `-AllowDirty`; tracked source must be clean before the script proceeds. |
| Commit hash recorded | PASS | Written to `release-manifest.json` by the formal build. |

## Version

| Item | Status | Notes |
| --- | --- | --- |
| `Directory.Build.props` `CwaVersion` | PASS | `0.9.0` |
| `ReleaseChannel` | PASS | `RC` |
| Desktop assembly/file version | PASS | Release build completed. |
| AutoCAD `PackageContents.xml` version | PASS | Synced by build script. |
| Installer filename/version | PASS | `CADWorkAssistant-Setup-0.9.0-RC-x64.exe`. |
| Manual/release notes version | PASS | 0.9.0 RC wording added. |

## Build And Tests

| Item | Status | Notes |
| --- | --- | --- |
| Clean | PASS | `scripts/build-release.ps1` |
| Restore | PASS | `scripts/build-release.ps1` |
| Release build | PASS | `CADWorkAssistant.CI.slnf`, 0 warnings, 0 errors. |
| Full tests | PASS | 449 passed, 0 failed. |
| Manual PDF generation | PASS | 28 pages generated and included. |

## Runtime Audit

| Item | Status | Notes |
| --- | --- | --- |
| No FakeAutoCad in user package | PASS | `scripts/audit-runtime.ps1` and `scripts/verify-distribution.ps1`. |
| No tests/source/PDB in distribution ZIP | PASS | `scripts/verify-distribution.ps1`. |
| No Node/Python/Playwright/Chromium/Ghostscript | PASS | Runtime audit. |
| No OpenAI/Anthropic/MCP/Claude runtime artifacts | PASS | Runtime audit. |
| No Autodesk host DLL redistribution | PASS | `acdbmgd.dll`, `acmgd.dll`, `accoremgd.dll` forbidden. |
| No firewall rule / port usage | PASS | Named Pipe only. |

## Installer

| Item | Status | Notes |
| --- | --- | --- |
| Setup exe exists | PASS | `CADWorkAssistant-Setup-0.9.0-RC-x64.exe`. |
| SHA256 generated | PASS | Setup and ZIP hashes generated. |
| Per-user install | PASS | `PrivilegesRequired=lowest`. |
| Running desktop app detected | PASS | AppMutex configured. |
| Running AutoCAD warning | PASS | Installer warns, never force-kills. |
| Uninstall preserves user data | PASS | Installer smoke retained `cadworkassistant.simulation.db`. |
| Reinstall preserves user data | PENDING | Uninstall data retention passed; upgrade/reinstall over a prior 0.8.0 artifact still needs a dedicated scenario. |

## Documentation

| Item | Status | Notes |
| --- | --- | --- |
| User manual PDF included | PASS | Installer and distribution folder. |
| README_FIRST included | PASS | `docs/releases/README_FIRST.txt`. |
| Release notes included | PASS | `docs/releases/RELEASE_NOTES_0.9.0-RC.md`. |
| Known limitations documented | PASS | Real AutoCAD validation pending. |
| SmartScreen/unsigned documented | PASS | Release notes and README_FIRST. |
| Third-party notices included | PASS | `THIRD_PARTY_NOTICES.txt`. |

## Clean Machine / Upgrade

| Item | Status | Notes |
| --- | --- | --- |
| Clean machine without SDK/VS/Node/Python | BLOCKED | Requires Windows Sandbox/VM or separate profile. |
| Previous installer upgrade | BLOCKED | No prior 0.8.0 artifact was confirmed in this session. |
| Uninstall/reinstall data retention | PENDING | Uninstall retention passed; reinstall-over-existing data retention still needs an explicit upgrade/reinstall scenario. |
| Offline launch | PASS | Smoke test launched installed desktop app in simulation mode without external services. |

## AutoCAD Validation

| Item | Status | Notes |
| --- | --- | --- |
| Milestone 8.5 | BLOCKED | Real AutoCAD validation pending. |
| Milestone 11B | BLOCKED | Real AutoCAD plot validation pending. |
| Milestone 12B | BLOCKED | Real AutoCAD text/Undo validation pending. |

## Final Decision

| Item | Status | Notes |
| --- | --- | --- |
| Distribution RC Ready | PASS | Formal clean-source RC package generated and verified. |
| 1.0 Production Ready | BLOCKED | Requires real AutoCAD validation gates. |
