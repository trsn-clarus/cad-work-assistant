# Architecture

## 1. 배경 조사 결과

개발을 시작하기 전 실제 개발 환경을 조사했다 (2026-08-08 기준).

| 항목 | 결과 |
|---|---|
| 설치된 AutoCAD | AutoCAD 2024 (내부 버전 24.3) |
| AutoCAD 2024 .NET 런타임 | **.NET Framework 4.8** (acdbmgd.dll, acmgd.dll, accoremgd.dll 확인) |
| .NET SDK | 최초 미설치 → 개발 진행을 위해 .NET 8 SDK 설치 |
| Visual Studio | 미설치 (dotnet CLI + SDK 기반으로 개발) |
| Git | 2.54.0 설치됨 |

**핵심 제약**: AutoCAD 2024는 .NET Framework 4.8 기반 Managed API를 제공한다. AutoCAD 2025부터 Autodesk가 .NET 8 기반으로 전환했지만, 현재 사용자 PC에는 2024가 설치되어 있으므로 **AutoCAD Plugin(In-process DLL)은 net48을 타겟으로 해야 한다.** 반면 Desktop 애플리케이션은 AutoCAD와 별도 프로세스이므로 이 제약을 받지 않는다.

이 비대칭성이 전체 아키텍처를 결정하는 가장 중요한 요인이다.

## 2. 프로세스 구조 — Two-Process Model

```text
┌─────────────────────────────┐         Named Pipe (JSON)        ┌──────────────────────────────┐
│  CADWorkAssistant.exe        │ <───────────────────────────────> │  acad.exe                      │
│  (Desktop, net8.0-windows)   │                                    │   └─ CADWorkAssistant.AutoCAD  │
│                               │                                    │        .dll (net48, in-proc)   │
│  WPF UI / MVVM                │                                   │   - IExtensionApplication      │
│  Project / Quantity / Export  │                                   │   - CWA_* Commands             │
└───────────────┬───────────────┘                                   │   - Pipe Server                │
                │                                                    └──────────────┬────────────────┘
                │ 참조                                                              │ 참조
                ▼                                                                    ▼
        ┌───────────────────────────────────────────────────────────────────┐
        │                CADWorkAssistant.Core (netstandard2.0)                │
        │   순수 C# 계산/도메인 로직. AutoCAD API, WPF 어느 쪽에도 의존하지 않음  │
        └───────────────────────────────────────────────────────────────────┘
                │                                            │
                ▼                                            ▼
   CADWorkAssistant.Infrastructure                 CADWorkAssistant.Documents
   (Logging / Settings / SQLite, netstandard2.0)    (Excel / PDF Export, netstandard2.0)
```

**왜 두 프로세스로 분리하는가**

1. AutoCAD 2024의 net48 제약을 Desktop UI까지 끌고 가지 않기 위해서다. WPF 자체는 net48에서도 동작하지만, 최신 .NET(성능, 보안 패치, 장기 지원)을 UI에 쓰고 싶다면 in-process 플러그인 방식만으로는 net48에 묶인다.
2. AutoCAD가 실행되어 있지 않아도 Desktop App은 켜져서 프로젝트/산출내역을 확인할 수 있어야 한다 (§17 계산 이력, §19 프로젝트 관리는 AutoCAD 없이도 열람 가능해야 함).
3. AutoCAD 버전이 바뀌어도(2024→2025→2026) Plugin DLL만 버전별로 다시 빌드하면 되고, Desktop App과 Core 로직은 그대로 재사용된다 (§29).
4. 향후 AutoCAD가 crash/hang 하더라도 Desktop App과 저장된 산출내역 데이터는 영향받지 않는다 (안정성, 데이터 유실 방지 원칙).

**Trade-off**: In-process 단일 DLL 방식보다 구현이 복잡하다(IPC 계층 필요). 이 비용은 Core에 IPC 계약(요청/응답 DTO)을 정의하고 Named Pipe + `System.Text.Json` 직렬화라는 단순한 조합으로 최소화한다. gRPC, WCF 등 무거운 프레임워크는 사용하지 않는다.

## 3. 프로젝트 구성과 책임

| 프로젝트 | TFM | 책임 | AutoCAD/WPF 의존성 |
|---|---|---|---|
| `CADWorkAssistant.Core` | netstandard2.0 | 단위 변환, 길이 계산(`Core/Length`), 도메인 모델, IPC 프로토콜(`Core/Ipc`)과 상태머신(`Core/Cad`). **AutoCAD 타입을 절대 참조하지 않는다** → AutoCAD 없이 유닛 테스트 가능 (§32) | 없음 |
| `CADWorkAssistant.Infrastructure` | **net48;net8.0** (멀티타겟) | 구조화 로깅(Serilog), 설정 저장(JSON), Named Pipe 전송 계층 전체(`Ipc/PipeMessageFramer`, `AutoCadPipeClient`, `AutoCadPipeServer`) | 없음 |
| `CADWorkAssistant.Documents` | netstandard2.0 | Excel/PDF/CSV 내보내기 (Milestone: Excel Export 단계에서 실제 구현) | 없음 |
| `CADWorkAssistant.Desktop` | net8.0-windows | WPF UI, MVVM, `Services/`(Discovery/ConnectionManager), `ViewModels/`(LengthWorkflowViewModel 등) | WPF |
| `CADWorkAssistant.AutoCAD` | net48 | AutoCAD Managed API 연동, IPC Handler(Ping/GetApplicationInfo/GetDrawingContext/SelectLengthObjects), 원본 DWG 보호/Undo 그룹 처리 | AutoCAD 2024 Managed API |
| `CADWorkAssistant.FakeAutoCad` (`tools/`) | net8.0 | AutoCAD 없이 개발/테스트하기 위한 Headless Simulation Host. `AutoCAD.Ipc.Handlers`와 **똑같은 IPC 프로토콜/서버 코드**를 재사용, Handler만 Scenario 기반 canned data로 교체. 설치 프로그램에 포함 안 함 (§73) | 없음 |
| `*.Tests` | net8.0 | Core/Infrastructure 로직 단위 테스트 + Integration.Tests는 FakeAutoCad를 실제 프로세스로 띄워 실제 Named Pipe로 검증 | 없음 (AutoCAD 미설치 환경에서도 실행 가능) |

Core/Documents가 `netstandard2.0`인 이유: net48(Plugin)과 net8.0(Desktop/FakeAutoCad) 양쪽에서 참조 가능한 가장 단순한 공통분모이기 때문이다. **Infrastructure만 예외적으로 `net48;net8.0` 멀티타겟이다** - Named Pipe에 현재 사용자 전용 ACL을 거는 방식이 두 런타임에서 서로 다르기 때문 (§9 의사결정 로그 참고). netstandard2.0을 기본값으로 유지하고, 실제로 막힌 경우에만 멀티타겟으로 전환한다 (§0 "불필요하게 복잡한 구조 지양").

## 4. AutoCAD 연동 계층

자세한 내용은 [AUTOCAD_INTEGRATION.md](./AUTOCAD_INTEGRATION.md) 참조. 핵심 원칙만 요약:

- **원본 보호**: 분석 전용 작업(길이/면적 조회 등)은 Transaction을 읽기 전용으로 열고 절대 `Database.SaveAs`/`qsave`를 자동 호출하지 않는다. 도면을 변경하는 작업(Export, Text 삽입 등)만 명시적 사용자 확인 후 진행한다 (§22).
- **Undo 그룹화**: AutoCAD 문서를 변경하는 모든 작업은 `Editor.StartUserInteraction` / Command 단위로 하나의 Undo 그룹으로 묶어 사용자가 AutoCAD에서 Ctrl+Z 한 번으로 되돌릴 수 있게 한다 (§23).
- **버전 독립성**: Plugin 프로젝트는 설치된 AutoCAD 경로를 자동 탐지(`AutoCAD.props`)하며, 특정 버전 경로를 하드코딩하지 않는다. 향후 AutoCAD 2025+(.NET 8) 지원이 필요해지면 `CADWorkAssistant.AutoCAD2025` 프로젝트를 추가하고 Core/Infrastructure는 그대로 재사용한다.

## 5. IPC 프로토콜 (Milestone 1에서 구현 완료)

호출 한 번이 실제로 지나가는 전체 경로:

```text
Desktop (net8.0-windows)
  MainWindowViewModel
        │ (PropertyChanged 구독)
        ▼
  AutoCadConnectionManager   ← Discover/Connect/Heartbeat/Reconnect 상태 머신 (CadConnectionStateEvaluator, 순수 함수)
        │
        ▼
  AutoCadDiscoveryService    ← Process.GetProcessesByName("acad") + 각 PID에 짧은 Ping으로 Plugin 존재 확인
        │
        ▼
  AutoCadPipeClient (Infrastructure.Ipc)  ← NamedPipeClientStream, 요청당 1개, 세마포어로 직렬화
        │
   ══════════════════ Named Pipe: CADWorkAssistant.AutoCAD.{PID} ══════════════════
        │  4-byte length prefix + UTF-8 JSON (PipeMessageFramer, Infrastructure.Ipc)
        ▼
  AutoCadPipeServer (AutoCAD Plugin, net48, acad.exe in-process)
        │
        ▼
  IpcRequestDispatcher (Core.Ipc)  ← ProtocolVersion 검증, MessageType → IIpcRequestHandler 라우팅, 예외를 IpcError로 변환
        │
        ▼
  IIpcRequestHandler (Ping / GetApplicationInfo / GetDrawingContext, AutoCAD.Ipc.Handlers)
        │
        ▼
  AutoCadDispatcher.InvokeAsync<T>  ← DocumentCollection.ExecuteInApplicationContext로 AutoCAD 메인 스레드에 안전하게 진입
        │
        ▼
  AutoCAD Managed API (Application.DocumentManager, Document, Database, LayoutManager)
```

- **전송**: `System.IO.Pipes` — 서버는 `NamedPipeServerStream`(현재 사용자만 접근 가능하도록 `PipeSecurity` 적용), 클라이언트는 `NamedPipeClientStream`.
- **Pipe 이름**: `CADWorkAssistant.AutoCAD.{AutoCAD 프로세스 ID}` — 다중 AutoCAD 인스턴스를 구분한다.
- **Framing**: 4-byte length prefix + UTF-8 JSON (`PipeMessageFramer`). "한 번의 Read = 메시지 하나"를 가정하지 않는다.
- **메시지**: `IpcRequestEnvelope`/`IpcResponseEnvelope`(Core.Ipc) — ProtocolVersion, RequestId(GUID), MessageType(문자열 상수), Payload(JSON), 실패 시 `IpcError`(Code/Message/TechnicalDetail).
- **AutoCAD API 스레드 경계**: Named Pipe accept-loop는 백그라운드 스레드지만, AutoCAD Managed API 호출은 전부 `AutoCadDispatcher`를 통해 `DocumentCollection.ExecuteInApplicationContext`로 마샬링한다 — 이 API의 실존 여부는 리플렉션으로 직접 확인했다 (docs/AUTOCAD_INTEGRATION.md §5.2).
- **연결 상태**: 단순 bool이 아니라 `CadConnectionState`(NoAutoCadProcess/ProcessDetected/PluginUnavailable/Connecting/Connected/Reconnecting/Disconnected/Faulted) — 상태 전이는 `CadConnectionStateEvaluator`라는 순수 함수로 분리해 AutoCAD/Named Pipe 없이 단위 테스트한다.
- **Heartbeat/Polling**: 2초 간격 (`IpcProtocol.HeartbeatIntervalMs`). `Ping`은 AutoCAD API를 전혀 건드리지 않아 AutoCAD 조작감(PAN/ZOOM 등)에 영향이 없도록 설계했다.
- **UI 스레드 marshaling**: `AutoCadConnectionManager`는 생성 시점(App.xaml.cs `OnStartup`, WPF UI 스레드)에 `SynchronizationContext.Current`를 캡처해두고, 모든 `PropertyChanged`를 그 컨텍스트로 `Post`한다. ViewModel은 Pipe 세부사항을 전혀 모른다.
- **검증**: `IpcRequestDispatcher`/`PipeMessageFramer`/`CadConnectionStateEvaluator`는 Core.Tests에서 AutoCAD 없이 단위 테스트했고, `AutoCadPipeClient`는 Integration.Tests에서 실제 Named Pipe로 Fake 서버 상대 종단간 테스트했다. 실제 AutoCAD GUI를 통한 연동 테스트는 이 개발 PC에서 그래픽 드라이버 불안정 문제로 완료하지 못했고, AutoCAD가 정상 동작하는 머신에서 진행하기로 했다 (docs/AUTOCAD_INTEGRATION.md §8).

## 6. Measurement Architecture (Length, Milestone 2)

Length가 Milestone 2의 첫 기능이지만, Area/Vertical Area/Parapet(§75)이 같은 모양으로 붙을 수 있도록 처음부터 책임을 나눠뒀다 - 아직 쓰지 않는 추상화 계층은 만들지 않되, 나눌 이유가 명확한 책임은 미리 나눴다:

```text
AutoCAD Plugin (SelectLengthObjectsHandler)
  - Editor.GetSelection으로 사용자 선택을 받는다
  - Curve.GetDistanceAtParameter로 원본 길이(RawLength, 도면 단위 그대로)를 구한다
  - 지원 안 하는 Geometry는 걸러서 ExcludedObjectTypeNames에 담는다
  - 합산도, 단위 변환도, 포맷팅도 하지 않는다 - 원본 데이터만 IPC로 보낸다
        │  LengthSelectionResponse { Objects, ExcludedObjectTypeNames, Unit }
        ▼
CADWorkAssistant.Core.Length (AutoCAD 비의존, 단위 테스트됨)
  - LengthUnitConverter: mm/cm/dm/m/km/inch/feet/yard/mile → m. Unitless/Other는 변환 실패를 값으로 반환한다(예외 아님)
  - LengthAggregationService: 여러 객체 합산 + 단위 변환 → LengthMeasurementResult
  - LengthFormatter: 표시용 반올림(기본 소수 3자리) - 내부 계산은 double 원본 정밀도 유지
        │
        ▼
Desktop.ViewModels.LengthWorkflowViewModel
  - IAutoCadConnectionManager.SendRequestAsync로 IPC 요청만 보낸다 (Pipe 세부사항을 모른다)
  - 응답을 Core.Length로 넘겨 집계하고, 결과를 Rows/TotalDisplay로 노출한다
  - 성공/취소/빈 선택/오류를 각각 다른 State로 구분한다 - 취소·빈 선택은 오류가 아니다
```

**AutoCAD Plugin과 FakeAutoCAD가 똑같은 프로토콜을 쓰는 이유**: `LengthSelectionResponse`/`CadLengthObjectDto`는 Core에 있고 AutoCAD 타입을 참조하지 않는다. AutoCAD Plugin의 Handler는 실제 Selection에서 이 DTO를 채우고, FakeAutoCAD의 Handler는 미리 정해둔 Scenario 데이터로 채운다 - Desktop과 Integration Test 입장에서는 둘을 구분할 수 없다 (실제로 `AutoCadConnectionManager`/`AutoCadPipeClient` 코드에 Fake 분기가 전혀 없다).

**Quantity Sheet와의 관계**: `LengthMeasurementResult`(방금 계산한 값)와 `QuantityRecord`(저장하기로 한 값)는 다른 타입이다. 사용자가 "산출내역 추가"를 눌러야 `QuantityRecord`로 변환되며, 이때 `RawValue`/`SourceUnit`/`ObjectHandles`/`CalculationExpression`을 함께 저장해 나중에 재검산할 수 있게 한다 (§28-29).

## 7. Desktop App 구조 (MVVM)

- `*.xaml` — 뷰 (구조/레이아웃/스타일), `Themes/DesignTokens.xaml`에 색상·타이포·spacing 토큰 정의
- `ViewModels/` — 자체 구현 `ObservableObject`/`RelayCommand` 기반, 상태와 커맨드
- `Services/` — `AutoCadDiscoveryService`(AutoCAD 프로세스 탐색), `AutoCadConnectionManager`(Discover/Connect/Heartbeat/Reconnect 상태 머신). ViewModel은 이 서비스의 인터페이스만 알고 Named Pipe/Process API를 직접 다루지 않는다.
- Navigation은 §27 정보구조를 기반으로 하되 실제 구현은 [`design-system/MASTER.md`](../design-system/MASTER.md)의 PROJECT/CAD/QUANTITY/OUTPUT/SETTINGS 그룹을 따른다. Milestone 0의 UI Shell은 더미 데이터로 채워진 상태이며, 실제 AutoCAD 연동은 Milestone 1부터 연결한다.
- UI 디자인 원칙(색상, spacing, 밀도, 안티패턴)은 `design-system/MASTER.md`가 단일 소스다 — ARCHITECTURE.md는 프로세스/데이터 구조를, design-system은 시각적 규칙을 다룬다.

## 8. 로깅 / 설정

- **Logging**: Serilog, 파일 싱크. 경로: `%LOCALAPPDATA%\CADWorkAssistant\logs\yyyy-MM-dd.log`, 일 단위 롤링. 도면 내부 좌표/치수 등 민감할 수 있는 상세 데이터는 Verbose 레벨에서만 기록하고 기본 레벨(Information)에는 요약만 남긴다 (§25).
- **Settings**: `%APPDATA%\CADWorkAssistant\settings.json`, `System.Text.Json` 직렬화. 소수점 자릿수(길이/면적 별도), 기본 단위 표시 등 사용자 환경설정을 저장한다 (§21).
- **Project/Quantity 데이터**: SQLite는 실제 Quantity Sheet 기능(Milestone 4) 구현 시점에 도입한다. 지금 시점에 스키마를 미리 설계하지 않는다 (§34 조기 구현 금지).

## 9. 의사결정 로그

| 결정 | 대안 | 선택 이유 |
|---|---|---|
| Desktop: net8.0-windows + WPF | net48 WPF (단일 프로세스) | 장기 지원, 최신 성능/보안 패치, AutoCAD 버전 제약에서 UI를 분리 |
| Plugin: net48 | net8.0 | 현재 설치된 AutoCAD 2024가 net48만 지원 |
| Core/Infra/Documents: netstandard2.0 | net8.0 멀티타게팅 | net48·net8 양쪽에서 참조 가능한 가장 단순한 공통분모 |
| MVVM: 자체 구현 (`ObservableObject`/`RelayCommand`, 30줄 내외) | CommunityToolkit.Mvvm, Prism | 이 규모의 앱에 필요한 MVVM 표면(`INotifyPropertyChanged`, `ICommand`)이 매우 작아 외부 의존성 없이 직접 구현. 필요 시점에 CommunityToolkit.Mvvm으로 교체 가능 (인터페이스 호환) |
| IPC: Named Pipe + JSON | gRPC, WCF, COM Automation | 의존성 최소, 방화벽/포트 이슈 없음, 로컬 프로세스 간 통신에 충분 |
| 로깅: Serilog | NLog, log4net | 구조화 로깅 표준, netstandard2.0 지원, 활발한 유지보수 |
| Test: xUnit | NUnit, MSTest | .NET 커뮤니티 표준, dotnet CLI 기본 템플릿과 궁합 |
| IPC Handler: `IIpcRequestHandler` 인터페이스 + Dispatcher의 Dictionary 라우팅 | 거대한 switch, MediatR | 명령이 늘어날 것(Length/Area/Selection/...)이 확실하므로 확장 지점을 미리 두되, MediatR 같은 프레임워크는 이 규모에 과함 (§39) |
| Desktop 의존성 구성: App.xaml.cs에서 직접 `new` (수동 composition root) | Microsoft.Extensions.DependencyInjection 등 DI 컨테이너 | 서비스 개수가 2~3개뿐이라 컨테이너 없이도 충분히 명확함 |
| AutoCAD API 스레드 마샬링: `DocumentCollection.ExecuteInApplicationContext` | `Application.Idle` 이벤트, `ExecuteInCommandContextAsync` | 리플렉션으로 실존을 확인한 API 중 읽기 전용 조회에 가장 가벼움. `ExecuteInCommandContextAsync`는 인터랙티브 명령(Selection 등)이 필요한 후속 Milestone을 위해 남겨둠 |
| Heartbeat(`Ping`)는 AutoCAD API를 전혀 호출하지 않음 | 매 heartbeat마다 `GetDrawingContext`로 통합 | 2초마다 AutoCAD 메인 스레드에 진입하는 것 자체가 조작감에 영향을 줄 수 있어, 연결 생존 확인과 문서 상태 조회를 분리 |
| `AutoCadPipeServer`를 AutoCAD 프로젝트에서 Infrastructure로 이동 | FakeAutoCad용 서버 코드를 별도로 새로 작성 | AutoCAD 타입을 전혀 참조하지 않는 순수 Named Pipe 코드였다 - 이동하면 AutoCAD Plugin과 FakeAutoCAD가 완전히 같은 서버 구현을 공유해서 프로토콜 불일치가 원천적으로 불가능해진다 |
| Named Pipe ACL: net8.0은 `PipeOptions.CurrentUserOnly`, net48은 수동 `PipeSecurity` | net8.0에도 수동 `PipeSecurity` + `NamedPipeServerStreamAcl.Create` | 실제로 시도했을 때 `NamedPipeServerStreamAcl.Create`+커스텀 `PipeSecurity` 조합이 `IOException("매개변수가 틀렸습니다")`를 냈다. `CurrentUserOnly`는 net48에 없어서 net48은 여전히 수동 구성이 필요하다 |
| `WaitForConnectionAsync` 취소 시 Pipe를 명시적으로 Dispose | CancellationToken만 전달 | 아무도 연결하지 않은 상태에서 취소해도 `WaitForConnectionAsync`가 실제로는 풀리지 않는 것을 실제로 겪었다 - `cancellationToken.Register`로 Pipe를 강제 Dispose해서 우회 |
| Length 계산: AutoCAD Plugin은 원본 데이터만 반환, 합산/변환/포맷팅은 Core.Length | Plugin에서 전부 계산해서 완성된 문자열만 보냄 | 계산 로직을 AutoCAD 없이 단위 테스트하기 위함 (§25, §32) - Plugin이 두꺼워지면 테스트 불가능한 로직이 늘어난다 |
| FakeAutoCad: `tools/`에 별도 실행 파일 프로젝트, 실제 서버 코드(Infrastructure.Ipc) 재사용 | Integration.Tests 안에 있는 in-process Fake만 계속 사용 | Milestone 1의 in-process Fake는 Transport 계층 테스트에는 충분하지만, Desktop을 실제로 띄워 UI까지 눈으로 확인하려면(§10, §55) 별도 프로세스가 필요하다. 기존 in-process Fake(`tests/.../Fakes/FakeAutoCadServer.cs`)는 그대로 남겨 Transport 단위 테스트에 계속 쓴다 |

## 10. 아직 결정하지 않은 것 (의도적으로 보류)

- SQLite 접근 방식(Raw ADO.NET vs Dapper vs EF Core) — Milestone 4 착수 시 결정
- Excel 라이브러리(ClosedXML 후보) — Excel Export 착수 시 라이선스/유지보수 재확인 후 결정
- PDF 라이브러리 — PDF Export 착수 시 결정 (QuestPDF는 회사 규모에 따라 상업 라이선스 필요할 수 있어 확인 필요)
- Installer(Inno Setup vs MSIX) — 첫 배포 가능한 빌드가 나온 뒤 결정
- AutoCAD 2025+(.NET 8) 지원 — 필요 시점에 별도 프로젝트로 추가
