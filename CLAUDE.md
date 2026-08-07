# CLAUDE.md

이 파일은 이 Repository에서 작업하는 Claude Code 세션을 위한 지침이다. 전체 요구사항 배경은 `docs/REQUIREMENTS.md`, 아키텍처 결정은 `docs/ARCHITECTURE.md`, 진행 상황은 `docs/ROADMAP.md`, AutoCAD 연동 세부사항은 `docs/AUTOCAD_INTEGRATION.md`를 참조한다.

## 프로젝트 한 줄 요약

AutoCAD 실무자가 매일 쓰는 Windows 설치형 업무 자동화 프로그램. DWG 확인 → 객체 선택 → 길이/면적/수량 산출 → 부분 추출/출력 → Excel/PDF 문서화까지 하나의 프로그램에서 처리한다.

## 절대 원칙

1. **원본 DWG를 임의로 저장/변경하지 않는다.** 읽기 전용 분석과 도면 변경 작업을 코드 레벨에서 명확히 구분하고, 변경 작업은 사용자 확인을 거친다.
2. **AutoCAD 문서를 변경하는 작업은 단일 Undo 그룹으로 묶는다.**
3. **계산 로직(`CADWorkAssistant.Core`)은 AutoCAD API를 참조하지 않는다.** AutoCAD 없이 유닛 테스트가 돌아가야 한다.
4. **사용자에게 원시 Exception/Stack Trace를 노출하지 않는다.** 이해 가능한 메시지 + "자세히 보기", Stack Trace는 로그 파일로.
5. **경로/AutoCAD 버전/특정 PC 설정을 하드코딩하지 않는다.**
6. **필요할 때 구현한다.** 다음 Milestone에서 쓸 기능을 미리 만들어두지 않는다 (`docs/ROADMAP.md` 순서를 따른다).

## 현재 확인된 개발 환경 (2026-08-08 기준, 변경되면 갱신할 것)

- 설치된 AutoCAD: **2024** (net48 기반 Managed API) → 이것 때문에 `CADWorkAssistant.AutoCAD` 프로젝트는 `net48`.
- 다른 AutoCAD 버전이 설치된 PC에서 작업하게 되면 `docs/AUTOCAD_INTEGRATION.md` §8 절차를 따라 버전별 프로젝트를 추가할지 판단할 것 — 기존 net48 프로젝트를 함부로 바꾸지 않는다.
- .NET SDK 8, Git 설치됨. Visual Studio는 없음 — `dotnet` CLI로 빌드/실행/테스트한다.

## 프로젝트 구조

```text
src/
  CADWorkAssistant.Core/            netstandard2.0 — 계산/도메인 로직, IPC DTO. AutoCAD·WPF 의존 금지
  CADWorkAssistant.Infrastructure/  netstandard2.0 — 로깅(Serilog), 설정(JSON), (추후) SQLite
  CADWorkAssistant.Documents/       netstandard2.0 — Excel/PDF/CSV export (필요 시점에 구현)
  CADWorkAssistant.Desktop/         net8.0-windows — WPF, MVVM(CommunityToolkit.Mvvm), 진입점
  CADWorkAssistant.AutoCAD/         net48 — AutoCAD Managed API, in-process plugin, Named Pipe 서버
tests/
  CADWorkAssistant.Core.Tests/          — Core 단위 테스트 (AutoCAD 불필요)
  CADWorkAssistant.Integration.Tests/   — AutoCAD 설치 환경에서만 실행되는 통합 테스트
design-system/   — UI 시각 규칙 단일 소스 (색상/타이포/spacing/컴포넌트/안티패턴). UI 작업 전 반드시 확인
docs/    — ARCHITECTURE / ROADMAP / REQUIREMENTS / AUTOCAD_INTEGRATION / UI_ENVIRONMENT_SETUP
installer/, samples/
```

MVVM은 외부 패키지 없이 직접 구현한 `ObservableObject`/`RelayCommand`(`src/CADWorkAssistant.Desktop/ViewModels/`)를 쓴다. CommunityToolkit.Mvvm 같은 패키지로 바꿀 필요가 생기기 전까지 추가하지 않는다.

Desktop(별도 프로세스)과 AutoCAD Plugin(in-process, net48)은 **Named Pipe + JSON**으로 통신한다. 자세한 프로토콜은 `docs/AUTOCAD_INTEGRATION.md` §5.

## 빌드 / 테스트

```powershell
dotnet build CADWorkAssistant.sln
dotnet test CADWorkAssistant.sln
dotnet run --project src/CADWorkAssistant.Desktop
```

`CADWorkAssistant.AutoCAD` 프로젝트는 로컬에 AutoCAD가 설치되어 있어야 빌드된다 (참조 DLL을 자동 탐지). CI에서는 `CADWorkAssistant.CI.slnf` (AutoCAD/Integration 제외)를 사용한다.

## 코딩 컨벤션

- Nullable reference types 활성화, `ImplicitUsings` 사용 (net48 프로젝트도 SDK 스타일이라 동일하게 적용됨).
- 단위 변환은 항상 `CADWorkAssistant.Core`의 변환 로직을 거친다 — Plugin이나 Desktop에서 mm/m 변환식을 직접 작성하지 않는다.
- Git 커밋은 의미 단위로 분리한다 (`feat:`, `fix:`, `refactor:` 등). 수십 개 기능을 한 커밋에 몰아넣지 않는다.
- 새 NuGet 의존성 추가 전: 유지보수 상태, 라이선스, .NET 호환성, 상업적 사용 가능 여부, AutoCAD 프로세스와의 충돌 가능성을 확인한다.

## 작업 방식

작업 시작 시 실제 파일을 읽고 현재 상태를 확인한다 (추측 금지). 변경 후에는 무엇을/왜/어떤 파일을/테스트 결과/남은 문제/다음 추천 작업을 간결히 보고한다.
