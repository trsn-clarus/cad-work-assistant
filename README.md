# CAD Work Assistant

AutoCAD 실무자를 위한 Windows 설치형 업무 자동화 프로그램. 설계 DWG 확인, 객체 선택, 길이·면적·수량 산출, 도면 일부 추출, 출력(Plot/PDF), 산출내역 문서화를 하나의 프로그램에서 처리한다.

> 개발 배경, 아키텍처 결정, 로드맵은 [`docs/`](docs/) 참조. 왜 Desktop App과 AutoCAD Plugin을 별도 프로세스로 분리했는지는 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), UI 디자인 방향은 [`design-system/MASTER.md`](design-system/MASTER.md) 참조.

## 요구사항

- Windows 10/11
- AutoCAD (Managed API 연동 대상 — 현재 개발/검증은 AutoCAD 2024 기준, `docs/AUTOCAD_INTEGRATION.md` 참조)
- .NET 8 SDK (Desktop App 빌드/실행, Core/Infrastructure/Documents 빌드)
- `CADWorkAssistant.AutoCAD` 프로젝트 빌드 시: 로컬에 설치된 AutoCAD 경로가 자동 탐지됨 (`docs/AUTOCAD_INTEGRATION.md` §2). 못 찾으면 `AUTOCAD_INSTALL_DIR` 환경변수를 설정한다.

## 프로젝트 구조

```text
src/
  CADWorkAssistant.Core/            netstandard2.0 — 계산·도메인 로직, 모델 (AutoCAD/WPF 비의존)
  CADWorkAssistant.Infrastructure/  netstandard2.0 — 로깅(Serilog), 설정(JSON)
  CADWorkAssistant.Documents/       netstandard2.0 — Excel/PDF/CSV export (구현 예정)
  CADWorkAssistant.Desktop/         net8.0-windows — WPF Desktop App (진입점), MVVM
  CADWorkAssistant.AutoCAD/         net48 — AutoCAD in-process plugin
tests/
  CADWorkAssistant.Core.Tests/          Core 단위 테스트 (AutoCAD 불필요)
  CADWorkAssistant.Integration.Tests/   AutoCAD 설치 환경에서만 의미 있는 통합 테스트
design-system/    UI 디자인 방향과 컴포넌트 규칙 (MASTER.md, pages/)
docs/             ARCHITECTURE / ROADMAP / REQUIREMENTS / AUTOCAD_INTEGRATION / UI_ENVIRONMENT_SETUP
installer/        설치 프로그램 스크립트 (추후)
samples/          테스트용 샘플 DWG (커밋 대상 아님)
```

## 빌드 / 실행 / 테스트

```powershell
dotnet restore CADWorkAssistant.sln
dotnet build CADWorkAssistant.sln
dotnet run --project src/CADWorkAssistant.Desktop
dotnet test CADWorkAssistant.sln
```

`CADWorkAssistant.AutoCAD`는 로컬에 AutoCAD가 설치되어 있어야 빌드된다. CI 등 AutoCAD가 없는 환경에서는 `CADWorkAssistant.CI.slnf`(AutoCAD/Integration.Tests 제외)를 사용한다:

```powershell
dotnet build CADWorkAssistant.CI.slnf
```

## 현재 상태

Milestone 0 (Foundation) — Solution/5개 프로젝트 스캐폴딩, Logging/Settings, WPF UI Shell(더미 데이터)까지 완료. 상세 진행 상황은 [`docs/ROADMAP.md`](docs/ROADMAP.md) 참조.
