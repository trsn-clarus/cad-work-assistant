# CLAUDE.md

이 파일은 이 Repository에서 작업하는 Claude Code 세션을 위한 지침이다. 전체 요구사항 배경은 `docs/REQUIREMENTS.md`, 아키텍처 결정은 `docs/ARCHITECTURE.md`, 진행 상황은 `docs/ROADMAP.md`, AutoCAD 연동 세부사항은 `docs/AUTOCAD_INTEGRATION.md`, AutoCAD 없이 개발/테스트하는 방법은 `docs/TESTING_WITHOUT_AUTOCAD.md`, 수량 조합(Vertical Area/Parapet) 공식/가정은 `docs/QUANTITY_COMPOSITION.md`, Project/Quantity/Activity 영속화(SQLite) 설계는 `docs/PERSISTENCE.md`, 수량 검산(Verification/Review) 철학/규칙은 `docs/QUANTITY_VERIFICATION.md`를 참조한다.

## 프로젝트 한 줄 요약

AutoCAD 실무자가 매일 쓰는 Windows 설치형 업무 자동화 프로그램. DWG 확인 → 객체 선택 → 길이/면적/수량 산출 → 부분 추출/출력 → Excel/PDF 문서화까지 하나의 프로그램에서 처리한다.

## 절대 원칙

1. **원본 DWG를 임의로 저장/변경하지 않는다.** 읽기 전용 분석과 도면 변경 작업을 코드 레벨에서 명확히 구분하고, 변경 작업은 사용자 확인을 거친다.
2. **AutoCAD 문서를 변경하는 작업은 단일 Undo 그룹으로 묶는다.**
3. **계산 로직(`CADWorkAssistant.Core`)은 AutoCAD API를 참조하지 않는다.** AutoCAD 없이 유닛 테스트가 돌아가야 한다.
4. **사용자에게 원시 Exception/Stack Trace를 노출하지 않는다.** 이해 가능한 메시지 + "자세히 보기", Stack Trace는 로그 파일로.
5. **경로/AutoCAD 버전/특정 PC 설정을 하드코딩하지 않는다.**
6. **필요할 때 구현한다.** 다음 Milestone에서 쓸 기능을 미리 만들어두지 않는다 (`docs/ROADMAP.md` 순서를 따른다).
7. **AutoCAD가 없어도 개발/테스트를 중단하지 않는다.** `CADWorkAssistant.FakeAutoCad`(실제 AutoCAD Plugin과 동일한 IPC 프로토콜/서버 코드를 쓰는 별도 프로세스)로 대부분의 기능을 종단간 검증할 수 있다 (`docs/TESTING_WITHOUT_AUTOCAD.md`).

## 현재 확인된 개발 환경 (2026-08-08 기준, 변경되면 갱신할 것)

- 설치된 AutoCAD: **2024** (net48 기반 Managed API) → 이것 때문에 `CADWorkAssistant.AutoCAD` 프로젝트는 `net48`.
- 다른 AutoCAD 버전이 설치된 PC에서 작업하게 되면 `docs/AUTOCAD_INTEGRATION.md` §9 절차를 따라 버전별 프로젝트를 추가할지 판단할 것 — 기존 net48 프로젝트를 함부로 바꾸지 않는다.
- .NET SDK 8, Git 설치됨. Visual Studio는 없음 — `dotnet` CLI로 빌드/실행/테스트한다.
- **이 PC에서 AutoCAD 2024 GUI를 실제로 띄우면 그래픽 드라이버가 불안정해진다** (Windows 이벤트 로그에 LiveKernelEvent 기록, Milestone 1에서 확인). AutoCAD Managed API 참조/컴파일은 정상 동작하므로 개발은 계속하되, NETLOAD 등 실제 GUI 연동 검증은 시도하기 전에 사용자에게 먼저 확인할 것 (`docs/AUTOCAD_INTEGRATION.md` §8). 대신 `CADWorkAssistant.FakeAutoCad`로 Headless 검증을 표준으로 쓴다.

## 프로젝트 구조

```text
src/
  CADWorkAssistant.Core/            netstandard2.0 — 계산/도메인 로직. Core/Ipc(프로토콜), Core/Cad(DTO+상태머신+단위 변환 계수), Core/Length(길이 집계/포맷), Core/Area(면적 분류/집계/포맷), Core/VerticalArea+Core/Parapet(수량 조합, AutoCAD IPC 없음), Core/Verification(수량 검산 - QuantityVerificationService/Context, Rule 9종, 범용 Rule Engine 아님). AutoCAD·WPF 의존 금지
  CADWorkAssistant.Infrastructure/  net48;net8.0 (멀티타겟) — 로깅(Serilog), 설정(JSON), Ipc/(PipeMessageFramer/AutoCadPipeClient/AutoCadPipeServer - 전송 계층 전체)
  CADWorkAssistant.Documents/       netstandard2.0 — Excel/PDF/CSV export (필요 시점에 구현)
  CADWorkAssistant.Persistence/     net8.0 (Desktop 전용, net48 Plugin은 참조 안 함) — SQLite(Microsoft.Data.Sqlite) 영속화. Migrations/(IMigration+DatabaseMigrator, PRAGMA user_version, Migration001/Migration002), Repositories/(Project/QuantityRecord/Activity/DrawingFile/ExportRecord/RecentMeasurement/QuantityVerification/QuantityReview 8쌍), CadWorkAssistantDatabase(연결+경로), ProjectDataService(교차 테이블 트랜잭션 조립)
  CADWorkAssistant.Desktop/         net8.0-windows — WPF, MVVM(자체 구현), Services/(Discovery/ConnectionManager/LengthSelectionCoordinator/ProjectContextService/QuantityVerificationCoordinator), ViewModels/(...QuantityHistoryViewModel), Views/(UserControl, HistoryPanel 포함), 진입점
  CADWorkAssistant.AutoCAD/         net48 — AutoCAD Managed API, in-process plugin, Ipc/Handlers/(Ping/GetApplicationInfo/GetDrawingContext/SelectLengthObjects/SelectAreaObjects) — Vertical Area/Parapet 전용 Handler 없음(기존 SelectLengthObjects 재사용)
tools/
  CADWorkAssistant.FakeAutoCad/     net8.0 — Headless AutoCAD Simulation Host (실행 가능 콘솔 앱). AutoCAD Plugin과 동일한 서버 코드 재사용. 설치본에 포함 안 함
tests/
  CADWorkAssistant.Core.Tests/          — Core+Infrastructure 단위 테스트 (AutoCAD 불필요)
  CADWorkAssistant.Persistence.Tests/   — 실제 파일 기반 SQLite로 Repository/마이그레이션/트랜잭션/재시작/다중 프로젝트 격리 테스트 (:memory: 아님, AutoCAD 불필요)
  CADWorkAssistant.Integration.Tests/   — FakeAutoCad를 실제 프로세스로 띄워 실제 Named Pipe로 종단간 테스트 (AutoCAD 불필요)
design-system/   — UI 시각 규칙 단일 소스 (색상/타이포/spacing/컴포넌트/안티패턴). UI 작업 전 반드시 확인
docs/    — ARCHITECTURE / ROADMAP / REQUIREMENTS / AUTOCAD_INTEGRATION / TESTING_WITHOUT_AUTOCAD / AUTOCAD_REAL_MACHINE_CHECKLIST / QUANTITY_COMPOSITION / PERSISTENCE / QUANTITY_VERIFICATION / UI_ENVIRONMENT_SETUP
installer/, samples/
```

MVVM은 외부 패키지 없이 직접 구현한 `ObservableObject`/`RelayCommand`(`src/CADWorkAssistant.Desktop/Common/`, `ViewModels/`)를 쓴다. CommunityToolkit.Mvvm 같은 패키지로 바꿀 필요가 생기기 전까지 추가하지 않는다.

Desktop(별도 프로세스)과 AutoCAD Plugin(in-process, net48)은 **Named Pipe + JSON**으로 통신한다. 자세한 프로토콜/호출 경로는 `docs/ARCHITECTURE.md` §5-6, `docs/AUTOCAD_INTEGRATION.md` §5.

새 AutoCAD 명령(Area 등)을 추가할 때: (1) `Core/Ipc/IpcMessageTypes.cs`에 상수 추가, (2) 필요하면 `Core/`에 요청/응답 DTO와 도메인 계산 로직(AutoCAD 비의존, 단위 테스트 가능하게) 추가, (3) `AutoCAD/Ipc/Handlers/`에 실제 Handler 구현, (4) `FakeAutoCad/Handlers/`에 대응하는 Fake Handler + `ScenarioCatalog`에 Scenario 추가, (5) `Extension.cs`/`FakeAutoCad/Program.cs` 양쪽의 handler 배열에 등록. AutoCAD 원본 API 사용 전에는 항상 리플렉션으로 실존을 확인한다 - 추측 금지.

새 "기존 측정값을 조합하는" 계산 기능(Vertical Area/Parapet처럼)을 추가할 때는 위 절차를 따르지
않는다 - 먼저 기존 IPC 명령(예: `SelectLengthObjects`)으로 필요한 원본 데이터를 이미 얻을 수 있는지
확인하고, 얻을 수 있다면 새 IPC 명령/AutoCAD Handler/FakeAutoCad Scenario를 만들지 않는다. `Core/`에
새 계산 로직만 추가하고 Desktop ViewModel에서 조합한다 (`docs/QUANTITY_COMPOSITION.md` 참고).

## 빌드 / 테스트

```powershell
dotnet build CADWorkAssistant.sln
dotnet test CADWorkAssistant.sln
dotnet run --project src/CADWorkAssistant.Desktop

# Simulation Mode로 Desktop 실행 (AutoCAD 없이 UI까지 확인)
tools\CADWorkAssistant.FakeAutoCad\bin\Debug\net8.0\CADWorkAssistant.FakeAutoCad.exe --scenario NormalSelection
$env:CWA_USE_FAKE_AUTOCAD = "1"; dotnet run --project src/CADWorkAssistant.Desktop
```

`CADWorkAssistant.AutoCAD` 프로젝트는 로컬에 AutoCAD가 설치되어 있어야 빌드된다 (참조 DLL을 자동 탐지). CI에서는 `CADWorkAssistant.CI.slnf`를 쓴다 - `CADWorkAssistant.AutoCAD`만 제외하고 `CADWorkAssistant.FakeAutoCad`/`Integration.Tests`를 포함한다(AutoCAD 없이도 빌드/테스트 가능하므로).

## 코딩 컨벤션

- Nullable reference types 활성화, `ImplicitUsings` 사용 (net48 프로젝트도 SDK 스타일이라 동일하게 적용됨).
- 단위 변환은 항상 `CADWorkAssistant.Core`의 변환 로직을 거친다 — Plugin이나 Desktop에서 mm/m, mm²/m² 변환식을 직접 작성하지 않는다. 단위→미터 계수 표는 `Core.Cad.DrawingUnitConversion`에 하나만 있고 `Core.Length`/`Core.Area`가 공유한다(Area는 제곱해서 씀) — 새 단위를 추가할 때 두 곳을 따로 고치지 않는다.
- AutoCAD Plugin의 Handler는 원본 데이터(도면 단위 그대로)만 IPC로 반환한다 — 합산/변환/포맷팅은 Core에서 한다 (테스트 가능성).
- Git 커밋은 의미 단위로 분리한다 (`feat:`, `fix:`, `refactor:` 등). 수십 개 기능을 한 커밋에 몰아넣지 않는다.
- 새 NuGet 의존성 추가 전: 유지보수 상태, 라이선스, .NET 호환성, 상업적 사용 가능 여부, AutoCAD 프로세스와의 충돌 가능성을 확인한다.
- 코드를 작성한 뒤에는 실제로 빌드/실행해서 검증한다 (컴파일 성공 ≠ 동작 확인). 이 프로젝트에서 실제로 겪은 예: `NamedPipeServerStreamAcl.Create`+커스텀 `PipeSecurity` 조합이 컴파일은 되지만 런타임에 `IOException`을 냈고, `WaitForConnectionAsync`는 컴파일상 CancellationToken을 받지만 실제로는 취소를 무시하는 경우가 있었다. `double.NaN`을 IPC payload에 담아 보내는 것도 컴파일은 문제없지만 `System.Text.Json`이 기본 설정으로는 NaN 직렬화에서 예외를 던진다 - Integration Test를 실제로 돌려서야 발견했다 (`IpcJson.Options`에 `JsonNumberHandling.AllowNamedFloatingPointLiterals` 필요). `DateTimeStyles.RoundtripKind`와 `DateTimeStyles.AssumeUniversal`을 같이 넘기는 것도 컴파일은 되지만 항상 `ArgumentException`을 던진다(둘은 상호 배타적) - Persistence 단위 테스트를 실제로 돌려서야 발견했다(Milestone 6). WPF에서 `Button`의 `AutomationProperties.Name`을 명시하지 않으면 UI Automation이 그 안의 자식 `TextBlock`(자기 Text가 자동으로 접근성 이름이 됨)을 먼저 찾아버려 `InvokePattern`이 없다는 예외가 난다 - Simulation Mode UI Automation 스크립트로 실제 클릭을 해봐야 드러났다. `TextBlock` 전용 스타일(`TargetType="TextBlock"`)을 XAML의 `Run` 요소에 지정하면 컴파일은 되지만 `XamlParseException`으로 앱이 창을 띄우기도 전에 죽는다(Milestone 7) - Run은 TextBlock이 아니라 TextElement 계열이다. `InverseBooleanToVisibilityConverter` 같은 bool 전용 Converter에 nullable 참조형을 직접 바인딩하면 컴파일은 되지만 `value is true` 패턴이 항상 false로 평가되어 Visibility가 항상 고정된다 - 반드시 진짜 `bool` 프로퍼티를 하나 더 만들어 바인딩한다. `DataGridCheckBoxColumn`의 `Mode=TwoWay` 바인딩은 컴파일/시각적 토글 모두 정상으로 보여도 특정 DataGrid 설정 조합에서 소스에 실제로 커밋되지 않을 수 있다(Milestone 7, 로그에 setter 호출 자체가 안 찍히는 것으로 확인) - 의심되면 `DataGridTemplateColumn` 안에 일반 `CheckBox`를 두는 방식이 더 안정적이다(DataGrid 셀 편집 생명주기를 타지 않고 자기 Click에서 즉시 커밋).

## 작업 방식

작업 시작 시 실제 파일을 읽고 현재 상태를 확인한다 (추측 금지). 변경 후에는 무엇을/왜/어떤 파일을/테스트 결과/남은 문제/다음 추천 작업을 간결히 보고한다.
