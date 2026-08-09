# CAD Work Assistant

AutoCAD 실무자를 위한 Windows 설치형 업무 자동화 프로그램. 설계 DWG 확인, 객체 선택, 길이·면적·수량 산출, 수량 검산, 도면 일부 추출/격리, 프로젝트 단위 SQLite 영속화를 하나의 프로그램에서 처리한다. TRSN CLARUS 제작.

> 개발 배경, 아키텍처 결정, 로드맵은 [`docs/`](docs/) 참조. 왜 Desktop App과 AutoCAD Plugin을 별도 프로세스로 분리했는지는 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), AutoCAD 없이 개발/테스트하는 방법은 [`docs/TESTING_WITHOUT_AUTOCAD.md`](docs/TESTING_WITHOUT_AUTOCAD.md), UI 디자인 방향은 [`design-system/MASTER.md`](design-system/MASTER.md), 설치 프로그램 빌드/배포는 [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) 참조.

## 설치형 프로그램으로 쓰기 (일반 사용자)

개발 환경 없이 그냥 쓰고 싶다면 `scripts\build-release.ps1`로 만든
`CADWorkAssistant-Setup-<version>-x64.exe` 하나만 있으면 된다.

```text
Setup 실행 (관리자 권한 불필요) → CAD Work Assistant 실행 → AutoCAD 2024가 있으면 자동 연결
```

Visual Studio, .NET SDK, git, Node, Python 중 아무것도 설치할 필요가 없다. 자세한 내용은
[`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md), 릴리스 절차는 [`docs/RELEASE_CHECKLIST.md`](docs/RELEASE_CHECKLIST.md) 참조.

## 개발 환경 요구사항

- Windows 10/11
- AutoCAD (Managed API 연동 대상 — 현재 개발/검증은 AutoCAD 2024 기준, `docs/AUTOCAD_INTEGRATION.md` 참조). **없어도 대부분의 기능을 개발/테스트할 수 있다** (`CADWorkAssistant.FakeAutoCad` 참고)
- .NET 8 SDK
- `CADWorkAssistant.AutoCAD` 프로젝트 빌드 시: 로컬에 설치된 AutoCAD 경로가 자동 탐지됨 (`docs/AUTOCAD_INTEGRATION.md` §2). 못 찾으면 `AUTOCAD_INSTALL_DIR` 환경변수를 설정한다.

## 프로젝트 구조

```text
src/
  CADWorkAssistant.Core/            netstandard2.0 — 계산·도메인 로직 (AutoCAD/WPF 비의존). Ipc(프로토콜)/Cad(상태머신/단위 변환)/Length/Area/VerticalArea+Parapet(수량 조합)/Verification(검산 엔진)/Models(Project·Quantity·Review 등)
  CADWorkAssistant.Infrastructure/  net48;net8.0 — 로깅(Serilog), 설정(JSON), Named Pipe 전송 계층
  CADWorkAssistant.Persistence/     net8.0 — SQLite(Microsoft.Data.Sqlite) 영속화. Migrations/Repositories/ProjectDataService
  CADWorkAssistant.Documents/       netstandard2.0 — Excel/PDF/CSV export (구현 예정)
  CADWorkAssistant.Desktop/         net8.0-windows — WPF Desktop App (진입점), MVVM, Assets/(앱 아이콘)
  CADWorkAssistant.AutoCAD/         net48 — AutoCAD in-process plugin
tools/
  CADWorkAssistant.FakeAutoCad/     net8.0 — Headless AutoCAD Simulation Host (AutoCAD 없이 개발/테스트용, 설치본에는 포함 안 됨)
tests/
  CADWorkAssistant.Core.Tests/          Core+Infrastructure 단위 테스트 (AutoCAD 불필요)
  CADWorkAssistant.Persistence.Tests/   실제 파일 기반 SQLite 테스트 (AutoCAD 불필요)
  CADWorkAssistant.Integration.Tests/   FakeAutoCad를 실제 프로세스로 띄워 실제 Named Pipe로 검증 (AutoCAD 불필요)
design-system/    UI 디자인 방향과 컴포넌트 규칙 (MASTER.md, pages/, PRODUCTION_UI_REVIEW.md)
docs/             ARCHITECTURE / ROADMAP / REQUIREMENTS / AUTOCAD_INTEGRATION / TESTING_WITHOUT_AUTOCAD / AUTOCAD_REAL_MACHINE_CHECKLIST / QUANTITY_COMPOSITION / PERSISTENCE / QUANTITY_VERIFICATION / DEPLOYMENT / RELEASE_CHECKLIST
installer/        CADWorkAssistant.iss (Inno Setup) + CADWorkAssistant.bundle/ (AutoCAD Plugin bundle manifest)
scripts/          build-release.ps1 (원커맨드 릴리스) / audit-runtime.ps1 / test-release.ps1
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

### 설치 프로그램 빌드 (한 커맨드)

```powershell
.\scripts\build-release.ps1
```

Clean → Restore → Build → Test → Publish(self-contained win-x64) → AutoCAD Plugin Bundle 준비 →
Runtime Dependency Audit → Installer(Inno Setup) → SHA256까지 한 번에 처리한다. 테스트가
실패하면 Installer는 만들어지지 않는다. 결과물은 `artifacts\installer\
CADWorkAssistant-Setup-<version>-x64.exe`. Inno Setup이 필요하다
(`winget install --id JRSoftware.InnoSetup -e`). 자세한 단계별 옵션은
[`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) 참조.

## 현재 상태

Milestone 8 (Production Packaging + Premium UI/UX Finalization) — 실제 설치형 Windows 제품으로
완성했다. `CADWorkAssistant-Setup-x.y.z-x64.exe` 하나로 설치/실행/AutoCAD 자동 연결/제거까지
전부 실제로 검증했고(Simulation Mode 기준 — 실제 AutoCAD GUI 연동은 이 개발 PC의 그래픽 드라이버
불안정 이력 때문에 별도 머신에서 진행), UI는 아이콘/입력 컨트롤 스타일 통일/Settings 화면 등으로
마무리했다. 이전 Milestone 7까지 Quantity History + Verification + Review, Milestone 6까지
SQLite Persistence + Project Management, Milestone 5까지 Drawing Navigation + Layer + Selection
+ WBLOCK이 이미 구현되어 있다. 상세 진행 상황은 [`docs/ROADMAP.md`](docs/ROADMAP.md), 배포 구조는
[`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) 참조.
