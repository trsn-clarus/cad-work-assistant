# AutoCAD Integration

## 1. 확인된 환경

- 설치 버전: AutoCAD 2024 (internal version 24.3.119.0), `C:\Program Files\Autodesk\AutoCAD 2024`
- Managed API 어셈블리: `acdbmgd.dll`, `acmgd.dll`, `accoremgd.dll` — **.NET Framework 4.8**
- AutoCAD 2025부터 Autodesk가 .NET 8 기반으로 전환했으나, 2024는 대상 아님. 따라서 `CADWorkAssistant.AutoCAD` 프로젝트는 `net48`을 타겟으로 한다.
- **주의 (2026-08-08)**: 이 개발 PC는 AutoCAD를 실무에 쓰는 머신이 아니며, AutoCAD 2024 GUI를 실제로 띄우면 그래픽 드라이버가 불안정해지는 것이 확인됐다 (§8 참고). Managed API 어셈블리 참조/컴파일은 정상 동작하므로 개발은 계속하되, GUI를 띄워야 하는 실제 연동 검증(NETLOAD, 화면 확인)은 AutoCAD가 정상 동작하는 다른 머신에서 진행한다.

## 2. Plugin 참조 방식

AutoCAD Managed API DLL은 **NuGet으로 배포되지 않고 로컬 설치 경로에서 직접 참조**한다 (Autodesk 공식 관행). `Private=False`/`CopyLocal=false`로 설정해 우리 설치 프로그램이 이 DLL들을 재배포하지 않도록 한다 — AutoCAD가 이미 제공하며, 라이선스상으로도 재배포 대상이 아니다.

경로는 하드코딩하지 않고 `src/CADWorkAssistant.AutoCAD/AutoCAD.props`에서 다음 우선순위로 탐지한다:

1. 환경변수 `AUTOCAD_INSTALL_DIR`
2. MSBuild 속성 `-p:AutoCADInstallDir=...`
3. `C:\Program Files\Autodesk\AutoCAD 2025\` → `2024\` → `2023\` 순으로 존재 여부 확인

세 방법 모두 실패하면 명확한 빌드 오류 메시지를 낸다 (§34 하드코딩 금지 원칙에 따라 "찾지 못하면 조용히 실패"가 아니라 "찾지 못하면 설정 방법을 알려주고 중단").

## 3. 로딩 방식

- **개발 중**: `NETLOAD` 명령으로 빌드된 DLL을 수동 로드
- **배포판**: `.bundle` 폴더 구조 + `PackageContents.xml` Autoloader 방식 채택 예정 (Milestone 1 이후 구체화). AutoCAD 시작 시 자동 로드되며, 설치 프로그램이 `%APPDATA%\Autodesk\ApplicationPlugins\` 아래에 배치한다.

## 4. 명령 이름 (CWA prefix)

| 명령 | 기능 | 도입 Milestone |
|---|---|---|
| `CWA` | 메인 Task Pane / 상태 확인 | 1 |
| `CWA_LENGTH` | 길이 산출 선택 모드 | 2 |
| `CWA_AREA` | 면적 산출 선택 모드 | 3 |
| `CWA_PARAPET` | 파라펫 계산 선택 모드 | 5 |
| `CWA_EXPORT` | 부분 DWG 추출 | 8 |
| `CWA_LAYER` | Layer 도구 | 9 |
| `CWA_PLOT` | Plot 프리셋 실행 | 10 |

`CWA` prefix는 흔한 AutoCAD 내장/서드파티 명령과 충돌 가능성이 낮아 채택했다. **아직 실제로 등록한 명령은 없다** - Milestone 3까지도 Desktop → IPC → Handler 경로가 유일한 진입점이고, AutoCAD 쪽 `[CommandMethod]`는 "Desktop 없이도 Plugin을 Smoke Test할 수 있게" 하는 부가 기능(Milestone 2 §47)이라 실제 AutoCAD에서 검증 가능해지는 시점(`docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`)에 추가하기로 미뤘다 - 등록하더라도 `SelectLengthObjectsHandler`/`SelectAreaObjectsHandler`와 같은 로직을 재사용하고 중복 구현하지 않는다.

## 5. IPC — Desktop ↔ Plugin (Milestone 1에서 구현 완료)

Desktop(net8.0, 별도 프로세스)과 Plugin(net48, `acad.exe` in-process)은 **Named Pipe**로 통신한다. 실제 구현 기준으로 정리하면:

- **서버**: `CADWorkAssistant.AutoCAD/Ipc/AutoCadPipeServer.cs`. `Extension.Initialize()`에서 시작하고 `Terminate()`에서 정리한다. 한 번에 클라이언트 하나를 받고, 연결이 끊기면 다시 새 연결을 기다린다 (Desktop 재시작 후에도 재연결 가능).
- **클라이언트**: `CADWorkAssistant.Infrastructure/Ipc/AutoCadPipeClient.cs`. WPF에 의존하지 않아 Integration.Tests에서 Fake 서버 상대로도 그대로 쓴다.
- **Pipe 이름**: `CADWorkAssistant.AutoCAD.{acad.exe PID}` (`IpcProtocol.GetPipeName`).
- **Framing**: `CADWorkAssistant.Infrastructure/Ipc/PipeMessageFramer.cs` — 4-byte length prefix(리틀 엔디언) + UTF-8 JSON. 최대 메시지 크기 1MB (`IpcProtocol.MaxMessageSizeBytes`).
- **Envelope**: `CADWorkAssistant.Core.Ipc.IpcRequestEnvelope` / `IpcResponseEnvelope` — `ProtocolVersion`(현재 1), `RequestId`(GUID), `MessageType`(문자열 상수, `IpcMessageTypes`), `Payload`(`JsonElement?`). 응답은 `Success` + `Payload` 또는 `Error`(`IpcError`: Code/Message/TechnicalDetail) 중 하나.
- **라우팅**: `IpcRequestDispatcher`(Core, AutoCAD 비의존) 가 `MessageType` → `IIpcRequestHandler` 로 라우팅한다. 프로토콜 버전 불일치, 알 수 없는 MessageType, Handler 예외를 모두 여기서 `IpcErrorCode`로 변환한다. AutoCAD 없이 Fake Handler로 단위 테스트한다 (`tests/CADWorkAssistant.Core.Tests/Ipc/`).
- **보안**: 서버 Pipe는 `PipeSecurity` + `WindowsIdentity.GetCurrent().User`로 현재 Windows 사용자만 접근 가능하도록 제한한다 (`AutoCadPipeServer.CreatePipe`).
- **Timeout**: Connect 1.5초, Request 3초 (`IpcProtocol`). 서버는 자체 타임아웃이 걸리면 `Cancelled`가 아니라 `Timeout` 오류로 정확히 구분해서 응답한다.

### 5.1 지원하는 요청

| MessageType | 요청 Payload | 응답 Payload | 비고 |
|---|---|---|---|
| `Ping` | 없음 | `{ pong, serverTimeUtc }` | AutoCAD API를 전혀 건드리지 않는다 - 2초 간격 heartbeat가 AutoCAD 조작감을 해치면 안 되기 때문 |
| `GetApplicationInfo` | 없음 | `AutoCadInstanceInfo`(Product, Version, ProcessId, PluginVersion, ProtocolVersion, IsSimulated) | |
| `GetDrawingContext` | 없음 | `DrawingContext`(DocumentDisplayName, FullPath, IsSaved, IsReadOnly, Layout, Units, DocumentCount) | 열린 문서가 없으면 `NoActiveDocument` 오류 |
| `SelectLengthObjects`(Milestone 2) | 없음 | `LengthSelectionResponse`(Objects, ExcludedObjectTypeNames, Unit) | Editor.GetSelection이 사용자 입력을 기다린다 - Command Context에서 실행 (§5.5). 집계/변환은 AutoCAD Plugin이 아니라 Core.Length가 한다 |
| `SelectAreaObjects`(Milestone 3) | 없음 | `AreaSelectionResponse`(Objects, ExcludedObjectTypeNames, Unit) | SelectLengthObjects와 같은 Command Context 경로(§5.5, §5.6). 분류(Valid/Open/Unsupported/InvalidGeometry)/합산/변환은 AutoCAD Plugin이 아니라 Core.Area가 한다 |

향후 명령은 `Core/Ipc/IpcMessageTypes.cs`에 상수를 추가하고 `CADWorkAssistant.AutoCAD/Ipc/Handlers/`에 `IIpcRequestHandler` 구현을 추가하는 것으로 확장한다 — 거대한 switch문을 두지 않는다.

### 5.2 AutoCAD API 스레드 경계 (AutoCadDispatcher)

Named Pipe 요청은 백그라운드 스레드(Accept Loop)에서 들어오지만, AutoCAD Managed API는 AutoCAD의 Application Context에서만 안전하다. 이 경계를 `CADWorkAssistant.AutoCAD/Ipc/AutoCadDispatcher.cs`가 담당한다.

**실제로 사용 가능한지 리플렉션으로 직접 확인한 API** (이 PC의 AutoCAD 2024 `accoremgd.dll` 기준, 추측 없이 검증함):

```
Autodesk.AutoCAD.ApplicationServices.DocumentCollection (accoremgd.dll)
  void ExecuteInApplicationContext(ExecuteInApplicationContextCallback callback, object data)
  DocumentCollection.ExecutionResult ExecuteInCommandContextAsync(Func<object, Task> callback, object data)
  Document MdiActiveDocument
  int Count

Autodesk.AutoCAD.ApplicationServices.Document (accoremgd.dll)
  string Name          // 저장된 문서는 전체 경로, 새 문서는 "Drawing1.dwg" 같은 이름
  bool IsNamedDrawing   // 저장된 적 있는지
  bool IsReadOnly
  Database Database

Autodesk.AutoCAD.DatabaseServices.Database (acdbmgd.dll)
  UnitsValue Insunits
  string Filename

Autodesk.AutoCAD.DatabaseServices.LayoutManager (acdbmgd.dll)
  static LayoutManager Current
  string CurrentLayout  // instance property

Autodesk.AutoCAD.ApplicationServices.Core.Application (accoremgd.dll)
  static DocumentCollection DocumentManager
  static Version Version   // "24.3.119.0" 형태 - 마케팅 연도("2024")는 주지 않는다
```

`AutoCadDispatcher`는 두 메서드를 제공한다. `InvokeAsync<T>`는 `ExecuteInApplicationContext`를 감싼 것으로 `GetApplicationInfo`/`GetDrawingContext` 같은 즉시-반환 조회에 쓴다. `InvokeInCommandContextAsync<T>`(Milestone 2에서 추가)는 `ExecuteInCommandContextAsync`를 감싼 것으로, `Editor.GetSelection`처럼 **사용자 입력을 기다리는** 작업에 쓴다 - Command Context 안에서 실행되어야 Selection/Prompt API가 안전하게 동작한다. 둘 다 내부적으로 같은 `TaskCompletionSource` 패턴을 쓴다.

### 5.5 Selection / Curve API (Milestone 2, `SelectLengthObjectsHandler`)

이 PC의 AutoCAD 2024 DLL에서 리플렉션으로 직접 확인한 API (추측 없음):

```
Autodesk.AutoCAD.EditorInput.Editor (accoremgd.dll)
  PromptSelectionResult GetSelection(PromptSelectionOptions options)

Autodesk.AutoCAD.EditorInput.PromptSelectionResult
  SelectionSet Value
  PromptStatus Status   // None, Modeless, Other, OK, Keyword, Cancel, Error

Autodesk.AutoCAD.EditorInput.SelectionSet
  ObjectId[] GetObjectIds()
  int Count

Autodesk.AutoCAD.DatabaseServices.Curve (acdbmgd.dll) - Line/Arc/Polyline/Polyline2d/Polyline3d 전부 이 클래스의 자식
  double GetDistanceAtParameter(double)
  double StartParam
  double EndParam
  // 직접 쓸 수 있는 "Length" 프로퍼티는 없다 - 길이는 반드시
  // GetDistanceAtParameter(EndParam) - GetDistanceAtParameter(StartParam)로 구한다

Autodesk.AutoCAD.DatabaseServices.Entity
  string Layer

Autodesk.AutoCAD.DatabaseServices.DBObject
  Handle Handle   // Handle.Value(long), ToString()으로 "2A7F" 같은 16진 문자열

Autodesk.AutoCAD.ApplicationServices.Document
  DocumentLock LockDocument()   // 매개변수 없는 오버로드

Autodesk.AutoCAD.DatabaseServices.Transaction
  DBObject GetObject(ObjectId id, OpenMode mode)

Autodesk.AutoCAD.DatabaseServices.OpenMode
  ForRead, ForWrite, ForNotify
```

구현 방침:

- `PromptStatus.Cancel` → `IpcErrorCode.SelectionCancelled` (오류 아님, §19). `PromptStatus.OK`인데 `SelectionSet.Count == 0`이면(사용자가 아무것도 선택하지 않고 Enter) 빈 `Objects` 배열로 정상 응답한다 - Desktop이 "선택된 객체가 없습니다"로 표시할지 결정한다 (§43).
- Line/Arc/Polyline/Polyline2d/Polyline3d 외의 Entity는 계산에서 제외하고 `ExcludedObjectTypeNames`에 타입 이름만 담는다 (§18) - 선택 자체는 막지 않는다(전체 선택 후 지원 객체만 계산 방식 채택).
- `Document.LockDocument()`로 Database/Editor 접근을 감싼다. Transaction은 `Commit()`을 호출하지 않는다 - Read-only이며(§61), `using`이 끝나면 자동 Abort된다.
- Unit 변환은 여기서 하지 않는다 - `RawLength`(도면 단위 그대로)만 담아 보내고, 합산/변환/포맷팅은 전부 `CADWorkAssistant.Core.Length`(AutoCAD 비의존, 단위 테스트됨)가 담당한다.

### 5.6 Area API (Milestone 3, `SelectAreaObjectsHandler`)

이 PC의 AutoCAD 2024 `acdbmgd.dll`을 리플렉션으로 직접 확인한 결과 (추측 없음):

```
Autodesk.AutoCAD.DatabaseServices.Curve (acdbmgd.dll)
  double Area      // Curve 자체에 선언되어 있다 - Polyline/Polyline2d/Polyline3d/Circle/Ellipse/Spline
                    // 전부 이 프로퍼티를 그대로 상속받는다 (재정의 없음)
  bool Closed       // 마찬가지로 Curve에 선언. Polyline/Polyline2d/Polyline3d는 자체 재정의를 갖고,
                    // Circle/Ellipse/Spline은 Curve의 구현을 그대로 쓴다

Autodesk.AutoCAD.DatabaseServices.Ellipse (acdbmgd.dll)
  double StartAngle
  double EndAngle
  double StartParam
  double EndParam
  // Ellipse 엔티티가 전체 타원이 아니라 타원 호(elliptical arc)도 표현할 수 있다는 뜻이다 -
  // 그래서 Closed 검사가 Circle과 달리 실제로 의미가 있다 (호는 Closed == false)

Autodesk.AutoCAD.DatabaseServices.Region (acdbmgd.dll)
  double Area   // Region 자신에 직접 선언 (Entity 파생, Curve 아님) - Region은 정의상 항상 닫힌 면이라
                // Closed 개념 자체가 없다

Autodesk.AutoCAD.Runtime.Exception (acdbmgd.dll)
  ErrorStatus ErrorStatus   // System.Exception 파생 - AutoCAD API 호출 실패 시 이 타입으로 던져진다
```

구현 방침:

- 지원 대상은 `Polyline`/`Polyline2d`(Curve.Area/Closed) + `Circle`/`Ellipse`(마찬가지) + `Region`(Region.Area, Closed 검사 불필요) 다섯 가지로 좁혔다. `Polyline3d`는 API 자체는 상속받지만 비평면 3D 형상의 면적 해석이 불확실하고, `Hatch`는 Associative/Pattern/Island 처리 복잡도가 높다 - 둘 다 실제 AutoCAD가 없어 이 PC에서 edge case를 검증할 수 없으므로 의도적으로 Unsupported 취급한다 (Milestone 3 §15, §43).
- `Curve.Area`/`Region.Area`를 읽는 동안 `Autodesk.AutoCAD.Runtime.Exception`(자기교차 등 비정상 형상에서 발생 가능)을 catch해서 `double.NaN`을 대신 담아 IPC로 보낸다 - AutoCAD 쪽에서만 재현 가능한 예외를 Core가 판단할 수 있는 신호로 바꿔주는 것이다. `Core.Area.AreaAggregationService`가 NaN/Infinity를 `InvalidGeometry`로 분류한다.
- Closed가 아닌 객체는 Area를 아예 읽지 않는다(`RawArea = 0`으로 응답) - 열린 Curve의 `.Area`를 읽는 것 자체가 어떤 값을 반환할지 문서화되어 있지 않고 실물로 검증할 수 없어, 애초에 접근하지 않는 쪽을 택했다.
- `Region`은 `Closed` 프로퍼티가 없으므로 `IsClosed: true`로 고정해 응답한다 - Region이라는 타입 자체가 "닫힌 면"이라는 의미이기 때문이다.

### 5.3 마케팅 버전 이름 (AutoCadVersionMap)

`Application.Version`과 `acad.exe`의 FileVersionInfo 모두 "AutoCAD 2024" 같은 마케팅 연도를 주지 않는다 (`ProductName`은 그냥 "AutoCAD", 버전은 "R24.3.119.0.0"). `AutoCadVersionMap`이 Autodesk의 공개된 릴리스 번호 체계를 바탕으로 한 매핑 표를 갖고 있으며, 매핑에 없는 버전은 절대 연도를 지어내지 않고 `AutoCAD (build {version})` 형태로 원본을 보여준다. 새 AutoCAD 버전이 나오면 이 표에 한 줄 추가하면 된다.

### 5.4 단위 매핑 (CadUnitMapper)

`UnitsValue`(AutoCAD) → `DrawingUnit`(Core, AutoCAD 비의존)로 변환한다. `UnitsValue.Undefined`는 `DrawingUnit.Unitless`로 매핑하며, mm를 임의로 가정하지 않는다 (§19). 이 매핑 함수는 AutoCAD 타입에 의존하기 때문에 AutoCAD 없이는 단위 테스트할 수 없다 - 대신 AutoCAD 참조 자체가 실제 설치본 기준으로 컴파일 검증되는 것으로 대체한다.

## 6. 원본 보호 / Undo 구현 방침

- **읽기 전용 작업** (길이/면적 조회 등): `Database.TransactionManager.StartTransaction()`을 읽기 용도로만 사용하고 `Commit()` 대신 `Dispose()`(자동 Abort)로 종료. `Database.SaveAs`/`qsave`는 어떤 경로로도 자동 호출하지 않는다.
- **변경 작업** (Text 삽입, Export 등): 실행 전 사용자 확인 UI를 거치고, `Editor.Command`/Transaction을 하나의 논리적 단위로 묶어 AutoCAD Undo 스택에 단일 항목으로 남도록 한다.
- 이 규칙은 코드 리뷰 체크리스트에도 반영한다 — Plugin 코드에서 `Commit()`을 호출하는 모든 지점은 "왜 도면을 변경해야 하는가"가 명확해야 한다.

## 7. 단위 처리

- 도면 단위는 `Database.Insunits`(`INSUNITS` 시스템 변수)로 조회한다. `UnitsValue.Millimeters`가 가장 흔하지만 하드코딩하지 않고 항상 조회한다 (실제 구현은 §5.4 `CadUnitMapper` 참고).
- Length/Area는 AutoCAD 내부 단위(도면 단위) 그대로 가져와 `CADWorkAssistant.Core`의 변환 로직에 넘긴다 — 변환 책임은 Plugin이 아니라 Core에 둔다 (테스트 가능성, §32). mm/m 변환은 Milestone 2(Length)에서, mm²/m² 변환은 Milestone 3(Area, 선형 계수를 제곱해서 재사용)에서 구현했다.

## 8. 실제 AutoCAD GUI 스모크 테스트 — 이 PC에서는 보류

Milestone 1에서 이 PC의 AutoCAD 2024 GUI를 실제로 띄우자 그래픽 드라이버가 불안정해지는 문제(Windows 이벤트 로그에 `LiveKernelEvent`/`BlueScreen` fault bucket 기록, 전체 재부팅은 없었음)를 확인했다. 사용자 확인 결과 이 PC는 AutoCAD 실사용 머신이 아니며, 개발은 계속하되 실제 GUI 연동 검증은 AutoCAD가 정상 동작하는 별도 머신에서 진행하기로 했다. Milestone 2, 3에서도 같은 제약이 이어진다.

대신 다음으로 검증을 대체했다 (`docs/TESTING_WITHOUT_AUTOCAD.md` 참고):

1. `CADWorkAssistant.AutoCAD` 프로젝트가 실제 AutoCAD 2024 Managed API 참조로 경고 0개/오류 0개로 빌드된다 (§5.5/§5.6의 API 목록은 전부 리플렉션으로 직접 확인).
2. `CADWorkAssistant.FakeAutoCad` - 실제 AutoCAD Plugin과 동일한 IPC 코드(Infrastructure.Ipc.AutoCadPipeServer, Core.Ipc)를 그대로 쓰는 별도 프로세스. Integration.Tests가 이 프로세스를 실제로 띄워 Named Pipe로 종단간 검증한다 (Milestone 3까지 총 29개 Scenario, Length 13개 + Area 16개).
3. Desktop을 Simulation Mode(`CWA_USE_FAKE_AUTOCAD=1`)로 FakeAutoCad에 붙여 실제 UI까지 수동으로 확인 - Length 선택 → "255.941 m" 표시, Area 선택 → "3,102.43 m²" 표시(4개 중 1개가 열려 있어 제외된 PartialSuccess 배너 포함) → 산출내역 추가까지 실제 두 프로세스 사이 통신으로 동작하는 것을 확인했다.

실제 AutoCAD GUI에서만 확인 가능한 항목은 [`AUTOCAD_REAL_MACHINE_CHECKLIST.md`](./AUTOCAD_REAL_MACHINE_CHECKLIST.md)에 전부 정리되어 있다 (현재 전부 Pending).

## 9. 향후 AutoCAD 버전 추가

새 버전(예: AutoCAD 2026, net8.0 기반) 지원이 필요해지면:

1. `src/CADWorkAssistant.AutoCAD2026/` 프로젝트 신규 생성 (다른 TFM 가능)
2. Command/IExtensionApplication 구현을 최대한 재사용하되, 버전별 API 차이는 각 프로젝트 안에서 흡수
3. `CADWorkAssistant.Core`/`Infrastructure`는 변경 없이 공유
4. Desktop App은 연결된 AutoCAD의 버전과 무관하게 동일한 IPC 프로토콜로 통신 (프로토콜 버전 필드를 두어 하위 호환 확인)

지금은 단일 버전(2024)만 지원하며, 이 구조는 실제로 두 번째 버전을 지원해야 하는 시점에 검증한다.
