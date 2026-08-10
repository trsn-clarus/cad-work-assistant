# Milestone 14 - Distribution Release Candidate

Status: formal clean distribution RC package generated (2026-08-10).

## Completed In This Pass

- Product version moved to `0.9.0` with `ReleaseChannel=RC`.
- Installer filename convention updated to `CADWorkAssistant-Setup-0.9.0-RC-x64.exe`.
- Release pipeline hardened: repository preflight, build/test/manual/publish/plugin/bundle/audit/installer/smoke/hash/distribution/ZIP.
- Distribution verification script added for required files, hashes, versions, and forbidden artifacts.
- Release notes, README_FIRST, third-party notices, and release gates documented.
- Settings/About exposes Version, Release Candidate, Data Location, User Manual, and Log Folder.
- Dirty validation pipeline passed with `scripts/build-release.ps1 -AllowDirty`.
- Formal release pipeline passed with `scripts/build-release.ps1` from committed clean tracked source.
- Installer smoke passed: silent install, installed files, user manual, AutoCAD bundle, desktop launch in simulation mode, uninstall, and user DB retention.
- Distribution verification passed for required files, setup hash, version content, manifest version/channel, and forbidden artifacts.
- Formal ZIP: `artifacts/release/CADWorkAssistant-0.9.0-RC-x64.zip`.

## Remaining Gate Checks

- Clean-machine or separate-profile install validation.
- Upgrade validation from a prior installer artifact if available.

## RC Scope

Desktop, project management, quantity workflows, history, verification, Excel/PDF/manual, installer, data retention, and offline packaging.

## Not 1.0

Milestone 8.5, 11B, and 12B remain blocked pending real AutoCAD validation.

## 1.0 Gate

RC gate + Milestone 8.5 PASS + Milestone 11B PASS + Milestone 12B PASS + no P0/P1 + signed installer strongly recommended + release regression pass.
