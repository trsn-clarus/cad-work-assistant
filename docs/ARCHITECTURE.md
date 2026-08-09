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
| `CADWorkAssistant.Core` | netstandard2.0 | 단위 변환, 길이 계산(`Core/Length`), 면적 계산(`Core/Area`), 수직면적/파라펫 수량 조합(`Core/VerticalArea`, `Core/Parapet`), **수량 검산(`Core/Verification`, Milestone 7)**, 도메인 모델, 표시명 정책(`Core/Models/QuantityTypeDisplay`, `QuantityReviewStatusDisplay`, `Core/Verification/VerificationSeverityDisplay` — Milestone 9, UI와 Excel이 같은 문구를 공유), IPC 프로토콜(`Core/Ipc`)과 상태머신(`Core/Cad`). **AutoCAD 타입을 절대 참조하지 않는다** → AutoCAD 없이 유닛 테스트 가능 (§32) | 없음 |
| `CADWorkAssistant.Infrastructure` | **net48;net8.0** (멀티타겟) | 구조화 로깅(Serilog), 설정 저장(JSON), Named Pipe 전송 계층 전체(`Ipc/PipeMessageFramer`, `AutoCadPipeClient`, `AutoCadPipeServer`) | 없음 |
| `CADWorkAssistant.Documents` | **net8.0** (Milestone 9에서 netstandard2.0 → net8.0 전환, `Persistence`와 같은 이유 — §8.8) | 공유 문서 모델(`Reports/QuantityReportModel`+`QuantityReportModelBuilder`+`IQuantityReportOptions`+`QuantityExportScope` — Milestone 10에서 Excel 전용이던 `QuantityWorkbookModel`을 일반화), 수량산출서 Excel Export(`Excel/QuantityWorkbookBuilder`, ClosedXML), 수량 산출근거 PDF Export(`Pdf/QuantityPdfBuilder`+`WindowsKoreanFontResolver`, PDFsharp-MigraDoc — Milestone 10, §8.9). CSV는 여전히 미착수 | 없음 (ClosedXML+PDFsharp-MigraDoc만, AutoCAD/WPF 없음) |
| `CADWorkAssistant.Persistence` | **net8.0만** (Infrastructure와 달리 net48 없음) | Project/QuantityRecord/ActivityRecord/DrawingFile/ExportRecord(**ExportType: DwgSelection/ExcelQuantity(M9)/PdfQuantityReport(M10)**)/RecentMeasurement/**QuantityVerificationSnapshot/QuantityReview(Milestone 7)** SQLite 영속화 (`Microsoft.Data.Sqlite`, raw ADO.NET). Migrations/(스키마 버전 관리), Repositories/(8쌍), `ProjectDataService`(교차 테이블 트랜잭션) | 없음 (Desktop만 참조, AutoCAD Plugin은 참조하지 않음 — §8.6) |
| `CADWorkAssistant.Desktop` | net8.0-windows | WPF UI, MVVM, `Services/`(Discovery/ConnectionManager/LengthSelectionCoordinator/ProjectContextService/**QuantityExcelExportCoordinator(M9)/QuantityPdfExportCoordinator(M10)/IQuantityReportSnapshotService(M10, 두 Coordinator가 공유하는 Persistence 조회)**), `ViewModels/`(LengthWorkflowViewModel, AreaWorkflowViewModel, VerticalAreaWorkflowViewModel, ParapetWorkflowViewModel, LengthSourceSelector, ProjectDialogViewModel, **ExcelExportViewModel, PdfExportViewModel** 등) | WPF |
| `CADWorkAssistant.AutoCAD` | net48 | AutoCAD Managed API 연동, IPC Handler(Ping/GetApplicationInfo/GetDrawingContext/SelectLengthObjects/SelectAreaObjects), 원본 DWG 보호/Undo 그룹 처리 | AutoCAD 2024 Managed API |
| `CADWorkAssistant.FakeAutoCad` (`tools/`) | net8.0 | AutoCAD 없이 개발/테스트하기 위한 Headless Simulation Host. `AutoCAD.Ipc.Handlers`와 **똑같은 IPC 프로토콜/서버 코드**를 재사용, Handler만 Scenario 기반 canned data로 교체. 설치 프로그램에 포함 안 함 (§73) | 없음 |
| `*.Tests` (`Core`/`Persistence`/`Documents`/`Integration`) | net8.0 | Core/Infrastructure/Documents 로직 단위 테스트 + Integration.Tests는 FakeAutoCad를 실제 프로세스로 띄워 실제 Named Pipe로 검증. Persistence.Tests는 실제 파일 SQLite, `ExcelExportE2ETests`(M9)/`PdfExportE2ETests`(M10)가 여기에 `CADWorkAssistant.Documents` 참조를 추가해 Project→Quantity→Verification→Review→Excel/PDF 전체 흐름을 검증한다. Documents.Tests/Persistence.Tests는 PDF 텍스트 검증 전용으로 PdfPig(테스트 전용, Apache-2.0)도 참조한다 | 없음 (AutoCAD 미설치 환경에서도 실행 가능) |

Core가 `netstandard2.0`인 이유: net48(Plugin)과 net8.0(Desktop/FakeAutoCad) 양쪽에서 참조 가능한 가장 단순한 공통분모이기 때문이다. **Infrastructure는 `net48;net8.0` 멀티타겟, Documents/Persistence는 net8.0 전용이다** - Documents/Persistence는 AutoCAD Plugin(net48)이 참조하지 않으므로 멀티타겟이 필요 없고, 오히려 net8.0 전용 NuGet 패키지(ClosedXML, Microsoft.Data.Sqlite)를 그대로 쓸 수 있다(§11 의사결정 로그 참고). netstandard2.0을 기본값으로 유지하고, 실제로 막힌 경우에만 net8.0 전용으로 전환한다 (§0 "불필요하게 복잡한 구조 지양").

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

## 7. Measurement Architecture (Area, Milestone 3)

Area는 §6의 Length와 완전히 같은 형태(Handler → Core 계산 → ViewModel)를 따르되, "닫혀 있는가"가 값 자체의 유효성을 좌우한다는 점이 Length와 다르다. 그래서 선택된 객체 전부(유효/열림/미지원/비정상형상)를 하나의 목록으로 표현하고, 각 항목에 상태를 붙인다:

```text
AutoCAD Plugin (SelectAreaObjectsHandler)
  - Editor.GetSelection으로 사용자 선택을 받는다 (Length와 동일한 InvokeInCommandContextAsync)
  - 면적 산출 Geometry(Polyline/Polyline2d/Circle/Ellipse/Region)인지 CadAreaGeometryMapper로 판별
  - Region이 아니면 Curve.Closed를 확인, 닫혀 있으면 Curve.Area(Region은 Region.Area)를 읽는다
  - Area를 읽다 AutoCAD 예외가 나면 catch해서 NaN을 담아 보낸다 - Core가 재현할 수 없는 예외를
    Handler가 먼저 판단해서 신호로 바꿔주는 것
        │  AreaSelectionResponse { Objects(CadAreaObjectDto: Handle/GeometryType/Layer/RawArea/IsClosed), ExcludedObjectTypeNames, Unit }
        ▼
CADWorkAssistant.Core.Area (AutoCAD 비의존, 단위 테스트됨)
  - AreaAggregationService.Classify: !IsClosed → Open, NaN/Infinity → InvalidGeometry,
    RawArea<0 → ArgumentException(방어적 검증), RawArea<=AreaEpsilon(1e-6) → InvalidGeometry(§17),
    나머지 → Valid로 분류해 AreaMeasurementItem 목록(Items)에 담는다
  - AreaUnitConverter: Length의 선형 계수를 제곱해서 재사용(mm² → m²는 1,000,000으로 나눈다, §21) -
    DrawingUnitConversion.MetersPerUnit을 Length/Area가 공유한다
  - AreaFormatter: 표시용 반올림(기본 소수 2자리, 천 단위 구분자)
        │
        ▼
Desktop.ViewModels.AreaWorkflowViewModel
  - Rows는 Valid 항목만 담는다 - 제외된 항목은 테이블에 행으로 넣지 않고 ExcludedSummary
    한 문장으로 요약한다("선택한 4개 객체 중 1개는 면적 계산에서 제외했습니다 (열린 형상 1개)")
    (design-system/pages/measurement-workspace.md §"왜 하나의 테이블/배너인가")
  - State가 8종(Idle/AwaitingSelection/Success/PartialSuccess/NoValidObjects/Cancelled/
    EmptySelection/Error)이라 Length보다 하나 더 많다 - "선택은 했지만 전부 무효"(NoValidObjects)와
    "아예 선택하지 않음"(EmptySelection)은 사용자에게 다른 메시지를 줘야 한다
```

**지원 Geometry를 좁게 잡은 이유**: Polyline/Polyline2d/Circle/Ellipse/Region은 실제 AutoCAD 2024
`acdbmgd.dll`을 리플렉션으로 확인한 결과 `Curve.Area`/`Curve.Closed`(Region은 `Region.Area`)를
안전하게 읽을 수 있음을 확인했다. Polyline3d는 같은 API를 상속하지만 비평면 3D 형상의 면적 해석이
불확실하고 실제 AutoCAD가 없어 검증할 수 없어 의도적으로 제외했다. Hatch는 Associative/Pattern/
Island 처리 복잡도가 높아 같은 이유로 이번 Milestone에서 제외했다 - 둘 다 추측으로 지원한다고
표시하지 않는다.

**Length와 Area가 공유하는 것**: `SelectionOutcome<TResponse>`(AutoCAD Plugin, 선택됨/취소/문서없음/
오류 4종 결과 래퍼), `DrawingUnitConversion.MetersPerUnit`(Core.Cad, 단위→미터 계수 표),
`SelectionCancelled`라는 하나의 `IpcErrorCode`, `AutoCadDispatcher.InvokeInCommandContextAsync`.
반대로 공유하지 않는 것: 집계/분류 로직 자체(Length는 단순 합산, Area는 4단계 분류 후 합산)와
UI ViewModel의 State enum(Area가 PartialSuccess/NoValidObjects로 더 세분화됨) - 억지로
하나의 제네릭 `MeasurementResult<T>`로 합치지 않았다. 두 기능의 계산 로직 자체가 다르기 때문에
공통화하면 오히려 각 기능의 단순함을 해친다.

## 8. Quantity Composition Architecture (Vertical Area, Parapet, Milestone 4)

Length/Area(§6-7)는 "AutoCAD에서 측정한 값을 보여준다"였다면, Vertical Area/Parapet은 처음으로
"측정한 값을 현장 조건과 조합해 공사 수량을 만든다" - 공식/단위 정규화/provenance의 자세한 내용은
[`QUANTITY_COMPOSITION.md`](./QUANTITY_COMPOSITION.md) 참고. 여기서는 호출 구조만 요약한다:

```text
AutoCAD Plugin - 변경 없음
  Vertical Area/Parapet 전용 IPC 명령이 없다. 기준 길이는 항상 Milestone 2의
  SelectLengthObjects로만 들어온다 (§5-6) - "새 AutoCAD Selection Handler를 불필요하게
  만들지 않는다"는 원칙을 그대로 지켰다.
        │
        ▼
Desktop.ViewModels.LengthSourceSelector (신규, Length/Area에는 없던 합성 컴포넌트)
  - CAD에서 새로 선택 / Length 도구의 최근 측정값 재사용 / 수동 입력, 세 가지 기준 길이
    확보 경로를 하나로 묶는다 - VerticalAreaWorkflowViewModel과 ParapetWorkflowViewModel이
    거의 동일한 로직을 각자 구현하면 명백한 중복이라 여기 하나로 뽑았다 (합성이지 상속이
    아니다 - 두 ViewModel의 나머지 상태/동작은 서로 다르기 때문에)
  - CAD 선택은 LengthSelectionCoordinator(공유 정적 헬퍼)를 거쳐 SelectLengthObjects IPC
    요청 → LengthAggregationService.Aggregate까지 수행하고 LengthMeasurementResult를
    돌려준다 - Length 도구가 하는 것과 완전히 같은 절차
        │
        ▼
CADWorkAssistant.Core.VerticalArea / Core.Parapet (AutoCAD 비의존, 단위 테스트됨)
  - VerticalAreaCalculator: A = L × H, 높이 단위 정규화·검증
  - ParapetCalculator: VerticalAreaCalculator를 측면/상부면 두 번 재사용해 조합 (§23)
        │
        ▼
Desktop.ViewModels.VerticalAreaWorkflowViewModel / ParapetWorkflowViewModel
  - HeightText 등 입력이 바뀔 때마다(TextBox UpdateSourceTrigger=PropertyChanged) 즉시
    재계산한다 - Length/Area처럼 "선택 → 결과" 한 번이 아니라 실시간 계산기에 가깝다
  - LengthSourceSelector.PropertyChanged를 구독해 기준 길이가 바뀔 때도 재계산한다
```

**Length/Area와 다른 점**: Length/Area는 State를 `LengthWorkflowState`/`AreaWorkflowState`
enum으로 표현했지만, Vertical Area/Parapet은 실시간 계산이라 "성공"이라는 순간이 따로 없다 - 값이
갖춰지면 그게 곧 결과다. 그래서 `IsReady`/`IsInvalidHeight`/`Source.IsBusy`/`Source.IsError` 같은
개별 bool로 표현했다 - 처음에는 별도 enum(`VerticalAreaWorkflowState`)을 만들었지만 실제로 쓰지
않는 상태가 대부분이라 구현 중 제거했다 (§62 "State enum을 늘리지 않는다"의 실제 적용 사례).

**LengthWorkflowViewModel에 생긴 변화**: `LastResult`(가장 최근 "성공"한 측정 결과, 화면에 지금
보이는 `_result`와는 다른 변수 - EmptySelection이면 화면은 비워지지만 LastResult는 마지막 성공값을
유지한다)를 노출하고, 값이 바뀔 때 `OnPropertyChanged(nameof(LastResult))`를 반드시 호출한다.
Simulation Mode 수동 검증 중 이 알림 호출이 빠져 있어서 "최근 측정값 사용" 라디오가 Length 쪽에서
새 측정이 끝나도 계속 비활성화 상태로 멈춰 있는 버그를 실제로 발견하고 고쳤다 - 계산된 값 자체는
맞았지만 WPF 바인딩이 그 사실을 몰랐던 경우다.

## 8.5 Drawing Navigation Architecture (Milestone 5)

Length/Area/Vertical Area/Parapet(§6-8)은 전부 "값 하나를 계산해서 보여준다"였다. Drawing
Navigation은 성격이 다르다 - 계산이 아니라 탐색/선택/일시적 표시 변경/파일 추출이 목적이다. 자세한
설계(IPC 명령 통합 근거, Isolation/Restore 정확성 보장 방식, WBLOCK 원리, Real AutoCAD 검증
대상)는 [`DRAWING_NAVIGATION.md`](./DRAWING_NAVIGATION.md) 참고. 여기서는 호출 구조만 요약한다:

```text
AutoCAD Plugin - 9개 신규 Handler (Ipc/Handlers/*.cs)
  GetDrawingOverview/ZoomExtents/ZoomToBounds - ApplicationContext(비인터랙티브)
  SelectDrawingObjects - CommandContext(Editor.GetPoint+GetCorner+SelectWindow/CrossingWindow,
    Length/Area의 GetSelection과 달리 사용자가 직접 두 모서리를 지정하는 인터랙션)
  IsolateObjects/SetLayerVisibility/RestoreVisibility - ApplicationContext, 셋이
    DrawingIsolationState(Plugin 내 공유 인스턴스)를 통해 "복원 = 변경 직전 정확한 상태"를 보장
  ExportSelection - ApplicationContext, Database.Wblock+SaveAs로 원본 Database 비수정
        │
        ▼
CADWorkAssistant.Core.Drawing (AutoCAD 비의존, 단위 테스트됨)
  - SelectionSession: 한 번의 선택 결과(Handle/타입별·Layer별 집계/합산 Bounds)를 담아
    Zoom/Isolate/Export가 재사용한다 - Selection을 반복하지 않는다
  - BoundsAggregator: 여러 Bounds의 union, NaN/Infinity 방어
  - ExportFileNameService: 파일명 제안("원본_설명.dwg")/Windows 금지문자 살균
        │
        ▼
Desktop.ViewModels.DrawingWorkflowViewModel (Navigation+Selection 통합, §80)
  ├── LayerWorkflowViewModel (조회/실제 동작하는 검색 필터/개별 토글/"선택 Layer만 보기")
  └── ExportWorkflowViewModel (설명 → 파일명 실시간 미리보기 → native SaveFileDialog)
```

**Length/Area/Parapet과 다른 점**: 이번 Milestone은 AutoCAD Managed API를 새로 9개나 쓰면서도
계산 로직은 거의 없다(Core.Drawing은 집계/문자열 유틸뿐) - 대신 "AutoCAD의 실제 표시 상태를 바꿨다가
정확히 되돌리는" 책임이 핵심이라, 상태를 어디서 스냅샷하고 언제 지우는지가 Length/Area의 "선택 →
계산 → 저장"보다 훨씬 중요하다(`DrawingIsolationState`, §45-46).

## 8.6 Persistence Architecture (Milestone 6)

§6-8.5는 전부 "AutoCAD에서 값을 가져온다"는 방향이었다. Persistence는 반대 방향이다 -
Desktop이 만든 데이터(Project/QuantityRecord/ActivityRecord/DrawingFile/ExportRecord/
RecentMeasurement)를 로컬 SQLite에 남겨 프로세스 재시작 후에도 유지한다. AutoCAD Managed
API를 전혀 새로 쓰지 않는 유일한 Milestone이다. 스키마/마이그레이션/트랜잭션/테스트 전략의
자세한 내용은 [`PERSISTENCE.md`](./PERSISTENCE.md) 참고, 여기서는 호출 구조만 요약한다:

```text
Desktop.ViewModels (Length/Area/VerticalArea/Parapet/Export WorkflowViewModel)
  │ RecordAdded / ExportCompleted 이벤트 (Project를 전혀 모름 - 기존 Milestone 2-5 패턴 그대로)
  ▼
Desktop.ViewModels.MainWindowViewModel
  │ 이벤트를 구독해 QuantityRecord.ProjectId를 채우고 IProjectContextService에 위임
  ▼
Desktop.Services.ProjectContextService (IProjectContextService)
  │ CurrentProject 없음 → 메모리 전용("빠른 세션")
  │ CurrentProject 있음 → ProjectDataService로 위임
  ▼
CADWorkAssistant.Persistence.ProjectDataService
  │ 커넥션+트랜잭션을 열어 여러 Repository를 조립 (예: QuantityRecord+ActivityRecord 원자적 저장)
  ▼
CADWorkAssistant.Persistence.Repositories.Sqlite*Repository (6쌍)
  │ 매 호출마다 SqliteConnection을 받는 상태 없는(stateless) 클래스
  ▼
CADWorkAssistant.Persistence.CadWorkAssistantDatabase
  │ 경로 결정(%LOCALAPPDATA%\CADWorkAssistant\data\, CWA_DATABASE_PATH override) + PRAGMA(WAL/
  │ foreign_keys/busy_timeout) + DatabaseMigrator.MigrateToLatest(PRAGMA user_version 기반)
  ▼
Microsoft.Data.Sqlite → cadworkassistant.db (WAL)
```

**다른 Milestone과 다른 점**: AutoCAD Managed API가 전혀 등장하지 않는다 - `DrawingFile` 등록에
쓰는 `FullPath`/`Units`조차 Milestone 1의 기존 `GetDrawingContext` 응답을 재사용한다. 대신 "DB
파일을 프로세스 재시작 사이에 정확히 보존한다"는 책임이 핵심이라, Level 1/2 테스트가 `:memory:`
DB가 아니라 실제 파일 기반이어야 의미가 있었다(`PERSISTENCE.md` §8) - 다른 Milestone들의
FakeAutoCad Headless E2E와 같은 위치를 실제 파일 SQLite가 대신한다.

## 8.7 Verification Architecture (Milestone 7)

저장된 QuantityRecord가 실제로 믿을 수 있는지 확인하는 계층. 자세한 규칙 9종/철학/발견한 버그는
[`QUANTITY_VERIFICATION.md`](./QUANTITY_VERIFICATION.md) 참고, 여기서는 호출 구조만 요약한다:

```text
Desktop.ViewModels.MainWindowViewModel
  │ QuantityRecord 저장 성공 직후 자동으로 빠른 검산 1건 실행(측정 도구는 여전히 Verification을
  │ 전혀 모른다 - Project를 모르는 것과 같은 원칙)
  ▼
Desktop.Services.QuantityVerificationCoordinator (IQuantityVerificationCoordinator)
  │ Core.Verification 실행 + Persistence 저장을 조립 - ProjectContextService와 같은 역할 분담
  ▼
CADWorkAssistant.Core.Verification (AutoCAD·DB 비의존, 순수 계산 - Length/Area/VerticalArea/
  Parapet과 같은 위치)
  - QuantityVerificationService: Rule 9종을 이름 있는 private 메서드로 구현(범용 Rule Engine 아님)
  - QuantityVerificationContext: 배치 검산 시 중복/비교/형상쌍 후보를 O(n)에 한 번만 색인
  - VerticalAreaCalculationMetadata/ParapetCalculationMetadata: 구조화 입력값을 JSON으로 보존해
    Rule 5(Formula Recompute)가 실제 Calculator를 다시 호출할 수 있게 한다
  ▼
CADWorkAssistant.Persistence.Repositories.SqliteQuantityVerificationRepository/
  SqliteQuantityReviewRepository (QuantityRecordId당 최신 상태만 upsert)
  ▼
Desktop.ViewModels.QuantityHistoryViewModel + Views.HistoryPanel
  └ QuantityHistoryRow - QuantityRecord + 최신 Verification + 최신 Review를 한 행에 묶는다
```

**다른 Milestone과 다른 점**: 이번에도 새 AutoCAD Managed API가 없다(Milestone 6과 같다) - 대신
"기존 계산 로직을 다시 신뢰할 수 있게 검증한다"는 책임이 핵심이라, 계산 재현의 정확성(Rule
4/5가 원본 계산부와 완전히 같은 부동소수점 연산 순서를 재현하는지)이 실제 AutoCAD 연동보다 더
중요한 검증 대상이었다. `VerificationSeverity`(자동 판정)와 `QuantityReviewStatus`(사용자 판단)를
분리된 두 축으로 설계한 것이 이 Milestone의 핵심 아키텍처 결정이다(§4).

## 8.8 Excel Quantity Export Architecture (Milestone 9)

저장된 QuantityRecord(+최신 Verification+Review)를 실무 제출/검토용 Excel 수량산출서로 내보내는
계층. 자세한 시트 구성/정밀도 정책/보안(수식 주입 방지)/atomic save는
[`EXCEL_EXPORT.md`](./EXCEL_EXPORT.md) 참고, 여기서는 호출 구조만 요약한다:

```text
Desktop.ViewModels.ExcelExportViewModel
  │ SaveFileDialog로 저장 경로 선택 → ExportAsync 호출 (Project를 모르는 측정 도구들과 달리
  │ 이 화면은 애초에 "현재 프로젝트"가 있어야만 동작한다 - IsExporting/IsSuccess/IsError는
  │ 기존 ExportWorkflowViewModel(Milestone 5, DWG WBLOCK)과 같은 bool-flag 관례)
  ▼
Desktop.Services.QuantityExcelExportCoordinator (IQuantityExcelExportCoordinator)
  │ Persistence에서 Project+QuantityRecord를 새로 읽고(캐시된 ObservableCollection을 신뢰하지
  │ 않는다 - 내보내기 시점의 DB가 source of truth), IQuantityVerificationCoordinator를 재사용해
  │ 최신 Verification/Review 딕셔너리를 얻는다(스냅샷 역직렬화 로직을 새로 만들지 않는다)
  ▼
CADWorkAssistant.Documents.Reports.QuantityReportModelBuilder (AutoCAD·ClosedXML 비의존, 순수 매핑 -
  Milestone 10에서 Excel 전용이던 QuantityWorkbookModelBuilder를 일반화, §8.9)
  │ Project+QuantityRecord[]+Verification/Review 딕셔너리+IQuantityReportOptions
  │   → QuantityReportModel/QuantityReportRow (Excel/PDF가 공유하는 순수 데이터 모델)
  │ 정렬은 항상 CreatedAt→Id 결정적 순서(DB 원본 순서에 의존하지 않는다), Verified-only 필터는
  │ 항상 QuantityReviewStatus 기준(자동 VerificationSeverity로 걸러내지 않는다 - Verified인데
  │ Error인 레코드도 그대로 노출)
  ▼
CADWorkAssistant.Documents.Excel.QuantityWorkbookBuilder (ClosedXML을 직접 다루는 유일한 클래스)
  │ 4개 시트 생성(수량산출서/산출근거/검산내역/프로젝트정보) + 인쇄 설정(A4 가로/1페이지 폭
  │ 맞춤/머리글 반복/쪽번호 바닥글) + SaveAtomically(임시 파일 → 재오픈 검증 → 원자적 교체)
  ▼
xlsx 파일 (사용자가 고른 경로) + Desktop.Services.IProjectContextService.AddExcelExportRecordAsync
  └ ExportRecord(ExportType=ExcelQuantity)+ActivityRecord를 한 트랜잭션에 저장, Dashboard의
    ObservableCollection에도 즉시 반영(재시작/프로젝트 전환 없이 Activity Log가 바로 갱신된다)
```

**다른 Milestone과 다른 점**: 이번 Milestone에서만 `CADWorkAssistant.Core`가 아닌
`CADWorkAssistant.Documents`가 새 외부 패키지(ClosedXML)를 직접 참조한다 - Core는 여전히
AutoCAD뿐 아니라 ClosedXML도 참조하지 않는다(§4 절대 원칙 3의 "AutoCAD API를 참조하지 않는다"를
넘어서, Core 전체가 "무엇을 출력하는지"를 몰라야 한다는 원칙으로 확장 적용했다). 사람이 읽는
계산식 문자열(`CalculationExpression`)은 Excel 셀에 **일반 문자열로만** 쓴다 - ClosedXML의
`FormulaA1`/`FormulaR1C1`을 쓰지 않으므로 `=`/`+`/`-`/`@`로 시작하는 사용자 입력(프로젝트명/설명/
검토메모)이 열렸을 때 Excel 수식으로 재해석되지 않는다. 이 성질은 4개의 실제 위험 문자열을
재오픈한 워크북에서 `cell.HasFormula == false`/`cell.DataType == XLDataType.Text`로 직접
검증했다(추정이 아니라 실증).

## 8.9 PDF Quantity Report Architecture (Milestone 10)

Excel(§8.8)이 만든 데이터 모델을 그대로 재사용해 제출/보고/보관용 고정 문서(PDF)를 만드는 계층.
자세한 보고서 구조/폰트/atomic save/보안은 [`PDF_EXPORT.md`](./PDF_EXPORT.md) 참고:

```text
Desktop.ViewModels.PdfExportViewModel  (ExcelExportViewModel과 완전히 같은 bool-flag 관례)
  ▼
Desktop.Services.QuantityPdfExportCoordinator (IQuantityPdfExportCoordinator)
  │ Desktop.Services.IQuantityReportSnapshotService를 QuantityExcelExportCoordinator와 공유한다 -
  │ Milestone 9에서 Excel Coordinator 안에 있던 "Persistence에서 새로 읽기" 로직을 이번에
  │ 별도 서비스로 뽑아냈다(§44) - 두 Coordinator가 정확히 같은 조회 결과를 본다(Cross-format
  │ consistency의 전제조건)
  ▼
CADWorkAssistant.Documents.Reports.QuantityReportModelBuilder (Excel Coordinator와 100% 같은 호출)
  ▼
CADWorkAssistant.Documents.Pdf.QuantityPdfBuilder (PDFsharp-MigraDoc을 직접 다루는 유일한 클래스)
  │ 표지/프로젝트요약/수량요약표 + 항목별 산출근거(각 QuantityReportRow를 산출식+검산+검토가
  │ 함께 있는 한 블록으로) + Header/Footer/페이지 번호 + SaveAtomically
  ▼
pdf 파일 (사용자가 고른 경로) + Desktop.Services.IProjectContextService.AddPdfExportRecordAsync
  └ ExportRecord(ExportType=PdfQuantityReport)+ActivityRecord - AddExcelExportRecordAsync와 같은 패턴
```

**다른 Milestone과 다른 점**: `Excel.QuantityWorkbookModel`을 `Documents.Reports.QuantityReportModel`로
일반화한 것이 이 Milestone의 핵심 리팩터링이다(§8.8이 "향후 PDF Export가 재사용할 수 있는" 모델로
이미 설계해뒀던 것을 실제로 검증한 순간이다) - Excel 회귀 테스트 49개가 이 리팩터링 전후로 전부
그대로 통과해야 했다(실제로 통과했다). PDFsharp가 .NET 8(비-GDI)에서 폰트를 전혀 모른다는 것과,
Windows의 기본 한글 폰트(맑은 고딕)에 ✓(U+2713) 글리프가 없어 PDF에서 빈 사각형으로 깨진다는
것은 둘 다 실제로 빌드/렌더링해봐야 드러난 문제였다(§4-2 문서, "컴파일 성공 ≠ 동작 확인"의 이번
Milestone 사례) - `WindowsKoreanFontResolver`와 PDF 전용 글리프 치환(`ToPdfSafeGlyph`)으로 해결했다.

## 9. Desktop App 구조 (MVVM)

- `*.xaml` — 뷰 (구조/레이아웃/스타일), `Themes/DesignTokens.xaml`에 색상·타이포·spacing 토큰 정의
- `ViewModels/` — 자체 구현 `ObservableObject`/`RelayCommand` 기반, 상태와 커맨드
- `Services/` — `AutoCadDiscoveryService`(AutoCAD 프로세스 탐색), `AutoCadConnectionManager`(Discover/Connect/Heartbeat/Reconnect 상태 머신). ViewModel은 이 서비스의 인터페이스만 알고 Named Pipe/Process API를 직접 다루지 않는다.
- Navigation은 §27 정보구조를 기반으로 하되 실제 구현은 [`design-system/MASTER.md`](../design-system/MASTER.md)의 PROJECT/CAD/QUANTITY/OUTPUT/SETTINGS 그룹을 따른다. Milestone 0의 UI Shell은 더미 데이터로 채워진 상태이며, 실제 AutoCAD 연동은 Milestone 1부터 연결한다.
- UI 디자인 원칙(색상, spacing, 밀도, 안티패턴)은 `design-system/MASTER.md`가 단일 소스다 — ARCHITECTURE.md는 프로세스/데이터 구조를, design-system은 시각적 규칙을 다룬다.

## 10. 로깅 / 설정

- **Logging**: Serilog, 파일 싱크. 경로: `%LOCALAPPDATA%\CADWorkAssistant\logs\yyyy-MM-dd.log`, 일 단위 롤링. 도면 내부 좌표/치수 등 민감할 수 있는 상세 데이터는 Verbose 레벨에서만 기록하고 기본 레벨(Information)에는 요약만 남긴다 (§25).
- **Settings**: `%APPDATA%\CADWorkAssistant\settings.json`, `System.Text.Json` 직렬화. 소수점 자릿수(길이/면적 별도), 기본 단위 표시 등 사용자 환경설정을 저장한다 (§21).
- **Project/Quantity 데이터**: SQLite(`%LOCALAPPDATA%\CADWorkAssistant\data\cadworkassistant.db`, WAL 모드), Milestone 6에서 구현 완료. 자세한 스키마/마이그레이션/트랜잭션 설계는 §8.6, [`PERSISTENCE.md`](./PERSISTENCE.md) 참고.

## 11. 의사결정 로그

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
| Area 지원 Geometry: Polyline/Polyline2d/Circle/Ellipse/Region만, Polyline3d/Hatch는 제외 | 리플렉션에 API가 보이는 대로 전부 지원 | Polyline3d는 API는 있지만 비평면 3D 형상의 면적 해석이 불확실하고, Hatch는 Associative/Island 처리 복잡도가 높다 - 둘 다 실제 AutoCAD가 없어 edge case를 검증할 수 없어 "확실하지 않으면 Unsupported로 제외"(Milestone 3 §15) 원칙을 그대로 적용했다 |
| `SelectionOutcome`을 `SelectionOutcome<TResponse>`로 제네릭화 | Area 전용으로 별도 타입 새로 작성 | Length Handler가 이미 쓰던 걸 그대로 복붙하면 Selected/Cancelled/NoActiveDocument/Error 네 가지 결과를 감싸는 코드가 완전히 중복된다 - 명확한 중복이라 공통화했다 (Milestone 3 §6) |
| `IpcJson.Options`에 `JsonNumberHandling.AllowNamedFloatingPointLiterals` 추가 | Area의 "면적 읽기 실패" 신호를 NaN이 아닌 다른 방식(예: nullable bool 플래그)으로 전달 | AutoCAD가 Area를 읽다 예외를 던진 상황을 NaN으로 전달하도록 설계했는데, `System.Text.Json`은 기본 설정으로 NaN 직렬화 시 예외를 던진다는 걸 Integration Test가 실제로 실행되며 드러났다(`AreaWorkflowEndToEndTests.FullWorkflow_InvalidGeometry_...`가 NullReferenceException으로 실패). IPC 계층 전체에 영향을 주는 근본 수정이 DTO를 새로 설계하는 것보다 작고 안전했다 |
| Area 총합 epsilon(1e-6) 이하는 InvalidGeometry로 분류 + 저장 버튼 비활성화 | 0인 값도 그대로 Valid로 합산 | 닫혀 있지만 면적이 사실상 0인 형상은 실무적으로 의미가 없고(Milestone 3 §17, §19), 저장 시점에 별도 확인 없이 걸러야 사용자가 빈 산출내역을 저장하는 실수를 하지 않는다 |
| Unit Override(Unitless 도면의 계산 단위 수동 지정)는 구현하지 않고 평가만 하고 보류 | Length/Area 양쪽에 바로 구현 | Milestone 3 §3 필수 기능에 없고, Project/Drawing 단위 영구 저장소가 필요한 별도 크기의 기능이다 - "필요할 때 구현한다"는 원칙에 따라 미룬다 (§12) |
| Vertical Area/Parapet 전용 AutoCAD IPC 명령을 만들지 않음 | `SelectVerticalAreaObjects`/`SelectParapetObjects` 신규 추가 | 둘 다 결국 "기준 길이 하나"가 필요할 뿐이고, 그건 이미 `SelectLengthObjects`가 준다(Milestone 4 §5-6) - Plugin 코드 변경이 이번 Milestone에서 거의 없다는 것 자체가 올바른 설계라고 판단했다(§104) |
| 기준 길이 확보(CAD 선택/최근 측정값/수동 입력) 로직을 `LengthSourceSelector`로 추출 | Vertical Area/Parapet ViewModel에 각자 구현 | 같은 Milestone 안에서 두 번째 소비자(Parapet)가 바로 나타나는 명백한 중복 사례라 상속이 아닌 합성으로 추출했다 - `SelectionOutcome<T>`(Milestone 3)와 같은 판단 기준 |
| Vertical Area/Parapet 결과 표시를 3자리로, Area는 2자리로 유지(같은 `AreaFormatter`를 다른 `decimalPlaces` 인자로 호출) | Area처럼 2자리로 통일 | Milestone 4 마스터 요구사항의 모든 실무값 예시(25.594/29.514/69.054)가 정확히 3자리였다 - 회귀 테스트로 고정된 값이라 임의로 바꿀 수 없었고, Area의 기존 2자리 결정(Milestone 3)도 그 나름의 근거가 있어 함께 바꾸지 않았다 |
| Vertical Area/Parapet Workflow State를 enum이 아니라 `IsReady`/`IsInvalidHeight` 등 개별 bool로 표현 | Length/Area처럼 `VerticalAreaWorkflowState` enum 사용 | 실시간 계산(버튼 클릭 없이 입력마다 재계산)이라 "성공 순간"이 따로 없다 - 실제로 enum을 먼저 만들었다가 구현 중 쓰이지 않는 상태가 대부분이라 제거했다(§62) |
| `LengthWorkflowViewModel.LastResult` 갱신 시 `OnPropertyChanged(nameof(LastResult))` 명시적 호출 추가 | 계산된 값만 맞으면 충분하다고 가정 | Simulation Mode 수동 검증 중 "최근 측정값 사용" 라디오가 Length 쪽 새 측정 완료 후에도 계속 비활성화 상태로 멈춰 있는 것을 실제로 발견했다 - `LastResult`가 PropertyChanged 없이 계산 전용 프로퍼티였던 것이 원인 |
| Persistence: `Microsoft.Data.Sqlite` raw ADO.NET | Entity Framework Core | 테이블 6개, 대부분 단순 CRUD - EF Core의 마이그레이션 도구/Change Tracking 오버헤드가 이 규모에서는 이득보다 비용이 크다. CommunityToolkit.Mvvm/MediatR을 거절한 것과 같은 기준(§39, "이 규모에 과함") |
| `CADWorkAssistant.Persistence`를 net8.0 전용 새 프로젝트로 분리 | 기존 `CADWorkAssistant.Infrastructure`(net48;net8.0)에 추가 | Infrastructure는 net48 AutoCAD Plugin이 참조한다 - Plugin이 SQLite 의존성을 갖게 되는 걸 원천 차단하려면 별도 프로젝트가 필요하다(마스터 프롬프트 §4, "AutoCAD Plugin → IPC → Desktop/Core → Persistence") |
| 스키마 버전 관리: `PRAGMA user_version` + `IMigration` 순서 적용 | 별도 SchemaVersion 테이블 | SQLite에 이미 내장된 정수 하나로 충분하다 - 별도 테이블은 그 자체로 하나의 스키마 결정이 더 필요해진다 |
| `decimal`은 TEXT(InvariantCulture), `DateTimeOffset`은 UTC ISO-8601 "O" TEXT로 저장 | `decimal`은 REAL, `DateTimeOffset`은 Unix timestamp(INTEGER) | SQLite REAL은 IEEE754 부동소수점이라 `255.941` 같은 실무 계산값의 정밀도가 깨질 위험이 있다(§143 기존 회귀 보호 대상과 같은 값 형태). ISO-8601 TEXT는 사전식 정렬이 곧 시간순 정렬이 되고 사람이 읽어도 바로 이해된다 |
| `ObjectHandlesJson`/`CalculationMetadataJson`을 JSON TEXT 컬럼으로 저장 | 각각 별도 child table (`QuantityRecordHandle`, `QuantityRecordMetadata`) | 지금 이 값들을 SQL로 개별 쿼리할 필요가 없고(§28), Vertical Area/Parapet처럼 필드가 늘어날 수 있는 구조라 스키마 유연성이 더 중요했다 |
| Repository는 `SqliteConnection`을 매 호출 인자로 받는 상태 없는 클래스, 커넥션은 작업마다 열고 닫음 | Repository가 장수명 커넥션을 필드로 소유 | SQLite 파일 연결은 비용이 낮고 WAL+busy_timeout으로 동시 접근을 다룰 수 있다 - 장수명 공유 커넥션을 여러 스레드(UI+백그라운드)에서 쓰는 스레드 안전성 복잡도를 피했다 |
| Project 삭제는 이번 Milestone에 구현하지 않음(스키마는 FK CASCADE로 대비) | QuantityRecord처럼 바로 구현 | 마스터 프롬프트 §170 필수 Acceptance Criteria 목록에 없다("필요할 때 구현한다" 원칙, CLAUDE.md 절대 원칙 6). QuantityRecord 삭제는 명시적으로 요구되어 구현했다 |
| "최근 측정값 사용"의 앱 재시작 후 DB 기반 자동 복구는 연결하지 않음(테이블/저장은 실제로 동작) | `LengthSourceSelector` 초기화 시 DB에서 자동 로드 | 마스터 프롬프트 §92 자체가 "구현한다면"이라는 조건부 표현이었다 - 확실히 요구된 범위가 아니다 |
| `DateTimeStyles.RoundtripKind`만 사용(AssumeUniversal 제거) | `RoundtripKind \| AssumeUniversal` 조합 | 실제로 조합해서 써봤더니 항상 `ArgumentException`(상호 배타적) - `ToDbText`가 이미 UTC Kind로 "O" 포맷을 쓰므로 문자열에 'Z'가 항상 붙어 RoundtripKind 하나로 충분했다. Persistence 단위 테스트를 실제로 돌려서 발견했다 |
| `VerificationSeverity`(자동 판정)와 `QuantityReviewStatus`(사용자 판단)를 완전히 분리된 두 축으로 설계 | 자동 검산 결과를 그대로 검토 상태로 취급(하나의 enum) | 사용자가 도면을 확인한 뒤 자동 Warning을 지우지 않고 "확인 완료" 메모만 남기고 싶어 할 수 있다(§8, §66) - 둘을 합치면 "왜 경고가 여전히 뜨는지" 사용자가 혼란스러워진다 |
| Heuristic Check(중복/비교/형상)는 최대 Review까지만, Error를 절대 선언하지 않음 | 중복이 확실해 보이면 Error로 승격 | 면적이 작은데 둘레가 더 긴 형상도 수학적으로 가능하다(§39-44) - 통계적/형상적 판단은 확정적 오류가 아니다. 자동으로 틀렸다고 단정하면 실제로는 정상인 복잡한 도형을 오류로 오인시킨다 |
| Vertical Area/Parapet 검산(Rule 5)은 문자열 `CalculationExpression`을 파싱하지 않고, 새 구조화 JSON 필드(`CalculationMetadataJson`)를 채워 실제 Calculator를 재호출 | 산식 문자열을 정규식으로 파싱해서 재계산 | 문자열은 사람이 읽기 위한 것이지 기계가 다시 계산하기 위한 것이 아니다(§25) - 파싱 로직 자체가 새로운 버그 표면이 된다. 이미 Milestone 6에 있었지만 아무도 채우지 않던 컬럼을 이번에 실제로 쓰기 시작했다 |
| `QuantityVerificationSnapshot`/`QuantityReview`는 upsert-latest-only(이력 없음) | 재검산/재검토마다 새 행을 append(감사 이력) | 10,000건 규모 프로젝트에서 반복 재검산 시 저장 공간이 무한정 늘어날 위험(§89) - "당시 검산 결과"의 실제 필요는 "마지막으로 확인했을 때"였다(§50 Stale 판정에만 쓰인다). RecentMeasurement/DrawingFile(Milestone 6)과 같은 패턴 |
| `QuantityVerificationContext`로 배치 검산 시 중복/비교/형상쌍 후보를 O(n) 한 번만 색인 | 레코드마다 전체 목록을 순회하며 비교(O(n²)) | 대규모 프로젝트(§89)에서 배치 검산이 느려지는 것을 피하기 위해 처음부터 이 구조로 설계했다 - "필요할 때 최적화한다"는 원칙의 예외는 성능 문제가 설계 초기부터 명백할 때다 |
| `DataGridCheckBoxColumn` 대신 `DataGridTemplateColumn` 안에 일반 `CheckBox` | `DataGridCheckBoxColumn` 유지, 다른 우회책 시도 | 실제로 UI Automation 클릭 검증 중 `DataGridCheckBoxColumn`의 TwoWay 바인딩이 이 화면의 DataGrid 설정 조합에서 커밋되지 않는 것을 발견했다(로그에 setter 호출 자체가 안 찍힘) - 일반 `CheckBox`는 DataGrid의 셀 편집 생명주기를 타지 않고 자기 Click에서 즉시 커밋해 더 안정적이다 |
| Excel 생성: ClosedXML(직접 OOXML 작성) | Microsoft.Office.Interop.Excel(COM), EPPlus | Interop은 실제 Excel 설치+프로세스 기동이 필요해 이 앱의 "설치형 프로그램이 스스로 문서를 만든다"는 목표와 배치되고 헤드리스 테스트가 사실상 불가능하다. EPPlus는 버전에 따라 상업 라이선스가 필요하다(NonCommercial 조건). ClosedXML은 MIT, 다운로드 수 많고 활발히 유지보수되며, Excel 설치 여부와 무관하게 파일 시스템에 직접 `.xlsx`를 쓸 수 있다 |
| `CADWorkAssistant.Documents`를 netstandard2.0 → net8.0으로 재타겟 | netstandard2.0 유지 + ClosedXML 버전을 netstandard2.0 호환 버전으로 고정 | Documents는 AutoCAD Plugin(net48)이 참조하지 않는다(§3) - `Persistence`가 이미 같은 이유로 net8.0 전용인 선례를 그대로 따랐다. net8.0 전용이면 ClosedXML 최신 버전을 제약 없이 쓸 수 있다 |
| Excel 셀에 사용자 입력 문자열을 `.Value=`/`.SetValue()`로만 쓰고 `FormulaA1`을 절대 쓰지 않음 | 산식 문자열(`CalculationExpression`)을 보기 좋게 만들려고 일부 셀만 수식으로 작성 | ClosedXML은 일반 값 대입에서는 OOXML `<f>` 요소를 만들지 않으므로 Excel이 열 때 재해석하지 않는다 - 프로젝트명/설명/검토메모처럼 사용자가 자유롭게 입력하는 문자열이 `=`/`+`/`-`/`@`로 시작해도 수식으로 실행되지 않는다(수식 주입 방지, §8.8). 4개 실제 위험 문자열로 재오픈 검증까지 마쳤다 |
| Excel Export 시나리오는 새 AutoCAD IPC 명령을 추가하지 않고, Persistence에 이미 저장된 QuantityRecord만 읽음 | AutoCAD에서 다시 선택하게 하거나 Export 시점에 도면을 재조회 | Vertical Area/Parapet(Milestone 4)과 같은 판단 기준 - Excel Export는 "기존 측정값을 조합"하는 것도 아니고 아예 "이미 저장된 값을 문서화"하는 것이라 AutoCAD 연동이 전혀 필요 없다. Plugin 코드가 이번 Milestone에서 전혀 바뀌지 않는다 |
| `ExportRecord`에 `ExportType`(DwgSelection/ExcelQuantity) 컬럼 추가, 기존 WBLOCK Export 호출부는 기본값으로 무변경 | Excel 전용 별도 테이블(`ExcelExportRecord`) 신설 | DWG 선택 내보내기(Milestone 5)와 Excel 내보내기는 둘 다 "사용자가 파일을 내보냈다"는 같은 개념이라 테이블을 분리할 이유가 없다 - 생성자 기본 인자로 기존 호출부(Migration 없이 컴파일만 다시 하면 그대로 동작)를 건드리지 않았다 |
| Dashboard Activity Log를 Excel 저장 직후 즉시 갱신하려고 `IProjectContextService.AddExcelExportRecordAsync`를 새로 추가(내부에서 `Activity.Insert(0, ...)`까지 수행) | `ProjectDataService`로 직접 저장만 하고 Activity 갱신은 다음 프로젝트 전환/재시작에 맡김 | Desktop의 `Activity` `ObservableCollection`은 `SwitchToAsync`에서만 다시 채워진다 - 저장 성공 후 사용자가 Dashboard를 봐도 방금 만든 Excel 기록이 안 보이면 "정말 저장됐나" 혼란을 준다. `AddQuantityRecordAsync`(기존)와 같은 패턴으로 맞췄다 |
| PDF 생성: PDFsharp-MigraDoc(공식 PDFsharp-Team 패키지) | QuestPDF | nuget.org 패키지 페이지를 직접 확인한 결과 QuestPDF는 연매출 $1M 미만 조직에만 무료인 Community License 조건이 있다 - 배포 대상 회사 규모를 알 수 없는 이 제품에는 위험하다고 판단했다. PDFsharp-MigraDoc은 조건 없는 순수 MIT이고, MigraDoc의 문서 모델(자동 페이지 나눔 포함)이 이 보고서 구조에 잘 맞는다 |
| `Excel.QuantityWorkbookModel`/`QuantityWorkbookModelBuilder`/`ExcelExportScope`를 `Documents.Reports.QuantityReportModel`/`QuantityReportModelBuilder`/`QuantityExportScope`로 일반화, `IQuantityReportOptions` 인터페이스 신설 | Excel 이름 그대로 유지하고 PDF가 그냥 재사용 | Milestone 9가 이미 "향후 PDF Export가 재사용할 수 있게" 설계해뒀던 모델이 실제로 두 번째 소비자(PDF)를 만나면서, Excel 전용 이름("Workbook")이 더 이상 맞지 않게 됐다 - 이름만 바꾸는 리팩터링이 아니라 네임스페이스도 렌더러 중립(`Documents.Reports`)으로 옮겨, 두 렌더러가 "같은 모델을 그대로 소비한다"는 사실이 코드 구조에서도 드러나게 했다. Excel 고유 옵션(시트 on/off)까지 억지로 공유하지는 않았다 |
| `IQuantityReportSnapshotService`를 Desktop.Services에 신설해 Excel/PDF Coordinator가 공유 | 각 Coordinator가 각자 Persistence 조회 로직을 유지 | Milestone 9의 `QuantityExcelExportCoordinator` 안에 있던 "Project+QuantityRecord를 새로 읽고 Verification/Review를 재사용" 로직을 PDF Coordinator가 그대로 다시 필요로 했다 - 이미 존재하던 중복을 없애는 리팩터링이지, "generic mega-export framework"를 새로 만든 것이 아니다 |
| Windows 시스템 폰트(맑은 고딕)를 실행 시점에 읽어 PDF에 임베드하는 자체 `IFontResolver` 구현 | 폰트 파일을 설치 프로그램에 번들링, 또는 PDFsharp의 기본 폰트 사용 | 실제로 빌드해서 렌더링해보니 PDFsharp 6.x(.NET 8 비-GDI)는 `IFontResolver`를 등록하지 않으면 즉시 예외를 던진다 - 이 앱은 이미 Windows 전용이므로 사용자 PC에 항상 있는 시스템 폰트를 그때그때 읽는 것이 폰트 라이선스 재배포 문제도 피하고 가장 단순하다 |
| PDF에서 검산 Pass 글리프를 ✓(U+2713) 대신 ○(U+25CB)로 치환(PDF 전용, Core/Excel은 무변경) | Core의 글리프 정책 자체를 바꾸거나, PDF에서 글리프를 아예 빼고 텍스트만 표시 | Simulation Mode에서 실제로 렌더링한 PDF를 육안으로 확인하다가 ✓ 글리프가 빈 사각형(tofu)으로 깨지는 것을 발견했다 - 맑은 고딕에 그 Dingbats 글리프가 없기 때문이다. 여러 후보를 실제로 렌더링해 비교한 뒤 ○가 정상 렌더링되고 한국어 문서의 ○/× 표기 관례와도 자연스러운 것을 확인해 PDF 렌더러 안에서만 치환했다 - Excel은 뷰어가 시스템 폰트로 자동 대체해 이미 문제없이 표시되므로 건드리지 않았다 |

## 12. 아직 결정하지 않은 것 (의도적으로 보류)

- Installer(Inno Setup vs MSIX) — 첫 배포 가능한 빌드가 나온 뒤 결정
- AutoCAD 2025+(.NET 8) 지원 — 필요 시점에 별도 프로젝트로 추가
- **Project/Drawing 단위 Unit Override**(§24-27, Unitless 도면에서 계산 단위를 사용자가 지정) — Milestone 3
  §3 필수 기능 목록에는 없고, 영구 저장소(Project/Drawing-scoped setting)가 필요한 별도 기능이라 평가만
  하고 미룬다. Unitless 도면은 지금과 같이 "자동 변환할 수 없다"고만 명확히 안내한다
- **독립 내측/외측 Parapet 길이**(Milestone 4 §30, §85) — 지금은 같은 기준 길이를 양면에 그대로
  적용하는 간편 산식만 지원한다. 벽 두께 때문에 실제 내/외측 둘레가 다른 경우를 정밀하게 계산하려면
  Advanced Parapet으로 별도 착수
- **개구부 공제**(Milestone 4 §87) — 창/문 등 개구부 면적을 자동으로 빼는 기능. Vertical Surface
  advanced feature로 남겨둔다
- **다구간(multi-segment) 높이 계산**(Milestone 4 §88) — 예: `10m×1m + 5m×0.5m`처럼 구간별로 다른
  높이를 적용. Multi-segment quantity composition으로 향후 확장 가능하게만 설계해뒀다
