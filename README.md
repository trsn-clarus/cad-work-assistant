# CAD Work Assistant

AutoCAD 실무자를 위한 Windows 설치형 업무 자동화 프로그램. 설계 DWG 확인, 객체 선택, 길이·면적·수량 산출, 도면 일부 추출, 출력(Plot/PDF), 산출내역 문서화를 하나의 프로그램에서 처리한다.

> 개발 배경, 아키텍처 결정, 로드맵은 [`docs/`](docs/) 참조. 왜 Desktop App과 AutoCAD Plugin을 별도 프로세스로 분리했는지는 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), AutoCAD 없이 개발/테스트하는 방법은 [`docs/TESTING_WITHOUT_AUTOCAD.md`](docs/TESTING_WITHOUT_AUTOCAD.md), UI 디자인 방향은 [`design-system/MASTER.md`](design-system/MASTER.md) 참조.

## 요구사항

- Windows 10/11
- AutoCAD (Managed API 연동 대상 — 현재 개발/검증은 AutoCAD 2024 기준, `docs/AUTOCAD_INTEGRATION.md` 참조). **없어도 대부분의 기능을 개발/테스트할 수 있다** (`CADWorkAssistant.FakeAutoCad` 참고)
- .NET 8 SDK
- `CADWorkAssistant.AutoCAD` 프로젝트 빌드 시: 로컬에 설치된 AutoCAD 경로가 자동 탐지됨 (`docs/AUTOCAD_INTEGRATION.md` §2). 못 찾으면 `AUTOCAD_INSTALL_DIR` 환경변수를 설정한다.

## 프로젝트 구조

```text
src/
  CADWorkAssistant.Core/            netstandard2.0 — 계산·도메인 로직 (AutoCAD/WPF 비의존). Ipc(프로토콜)/Cad(상태머신/단위 변환)/Length(길이 계산)/Area(면적 계산)
  CADWorkAssistant.Infrastructure/  net48;net8.0 — 로깅(Serilog), 설정(JSON), Named Pipe 전송 계층
  CADWorkAssistant.Documents/       netstandard2.0 — Excel/PDF/CSV export (구현 예정)
  CADWorkAssistant.Desktop/         net8.0-windows — WPF Desktop App (진입점), MVVM
  CADWorkAssistant.AutoCAD/         net48 — AutoCAD in-process plugin
tools/
  CADWorkAssistant.FakeAutoCad/     net8.0 — Headless AutoCAD Simulation Host (AutoCAD 없이 개발/테스트용, 설치본에는 포함 안 됨)
tests/
  CADWorkAssistant.Core.Tests/          Core+Infrastructure 단위 테스트 (AutoCAD 불필요)
  CADWorkAssistant.Integration.Tests/   FakeAutoCad를 실제 프로세스로 띄워 실제 Named Pipe로 검증 (AutoCAD 불필요)
design-system/    UI 디자인 방향과 컴포넌트 규칙 (MASTER.md, pages/)
docs/             ARCHITECTURE / ROADMAP / REQUIREMENTS / AUTOCAD_INTEGRATION / TESTING_WITHOUT_AUTOCAD / AUTOCAD_REAL_MACHINE_CHECKLIST
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

`CADWorkAssistant.AutoCAD`는 로컬에 AutoCAD가 설치되어 있어야 빌드된다. CI 등 AutoCAD가 없는 환경에서는 `CADWorkAssistant.CI.slnf`(AutoCAD 프로젝트만 제외, FakeAutoCad/Integration.Tests는 포함)를 사용한다:

```powershell
dotnet build CADWorkAssistant.CI.slnf
dotnet test CADWorkAssistant.CI.slnf
```

### AutoCAD 없이 UI까지 확인하기 (Simulation Mode)

```powershell
# 터미널 1
tools\CADWorkAssistant.FakeAutoCad\bin\Debug\net8.0\CADWorkAssistant.FakeAutoCad.exe --scenario NormalSelection

# 터미널 2
$env:CWA_USE_FAKE_AUTOCAD = "1"
dotnet run --project src/CADWorkAssistant.Desktop
```

자세한 내용과 시나리오 목록은 [`docs/TESTING_WITHOUT_AUTOCAD.md`](docs/TESTING_WITHOUT_AUTOCAD.md) 참조.

## 현재 상태

Milestone 3 (Area) — AutoCAD 영역 선택 → 닫힘 확인 → 면적 추출 → 단위 변환 → 합산 → 결과 표시 → 산출내역 저장까지 구현 완료. Length(Milestone 2)와 같은 패턴을 공유하며, Headless Simulation으로 종단간 검증 완료, 실제 AutoCAD GUI 검증만 남음 (`docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`). 상세 진행 상황은 [`docs/ROADMAP.md`](docs/ROADMAP.md) 참조.
