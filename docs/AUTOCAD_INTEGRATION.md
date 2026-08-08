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

`CWA` prefix는 흔한 AutoCAD 내장/서드파티 명령과 충돌 가능성이 낮아 채택했다. 실제 등록 전 `(command-list)` 또는 각 명령 등록 시 AutoCAD가 충돌을 알려주므로, Milestone 1에서 실제 등록하며 재확인한다.

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

### 5.1 지원하는 요청 (Milestone 1)

| MessageType | 요청 Payload | 응답 Payload | 비고 |
|---|---|---|---|
| `Ping` | 없음 | `{ pong, serverTimeUtc }` | AutoCAD API를 전혀 건드리지 않는다 - 2초 간격 heartbeat가 AutoCAD 조작감을 해치면 안 되기 때문 |
| `GetApplicationInfo` | 없음 | `AutoCadInstanceInfo`(Product, Version, ProcessId, PluginVersion, ProtocolVersion) | |
| `GetDrawingContext` | 없음 | `DrawingContext`(DocumentDisplayName, FullPath, IsSaved, IsReadOnly, Layout, Units, DocumentCount) | 열린 문서가 없으면 `NoActiveDocument` 오류 |

향후 명령(`SelectObjects`, `GetLength` 등)은 `Core/Ipc/IpcMessageTypes.cs`에 상수를 추가하고 `CADWorkAssistant.AutoCAD/Ipc/Handlers/`에 `IIpcRequestHandler` 구현을 추가하는 것으로 확장한다 — 거대한 switch문을 두지 않는다.

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

`AutoCadDispatcher.InvokeAsync<T>(Func<T> operation, CancellationToken)`은 `ExecuteInApplicationContext`를 `TaskCompletionSource`로 감싼 것이다. Milestone 1의 읽기 전용 조회(`GetApplicationInfo`, `GetDrawingContext`)에는 이것으로 충분하다. 향후 Selection처럼 인터랙티브한 명령이 필요해지면 `ExecuteInCommandContextAsync`(문서 Command Context 안에서 실행, `Editor.GetSelection` 등이 가능해짐)를 추가로 도입한다 - 지금은 쓰지 않는다.

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
- Length/Area는 AutoCAD 내부 단위(도면 단위) 그대로 가져와 `CADWorkAssistant.Core`의 변환 로직에 넘긴다 — 변환 책임은 Plugin이 아니라 Core에 둔다 (테스트 가능성, §32). 실제 mm/m 변환 로직은 Milestone 2(Length)에서 구현한다.

## 8. 실제 AutoCAD GUI 스모크 테스트 — 이 PC에서는 보류

Milestone 1 구현 자체(IPC 프로토콜, Named Pipe 서버/클라이언트, AutoCAD Dispatcher, Handler)는 다음 두 가지 방법으로 검증했다:

1. `CADWorkAssistant.AutoCAD` 프로젝트가 이 PC에 설치된 **실제 AutoCAD 2024 Managed API 참조**로 경고 0개/오류 0개로 빌드된다 (§5.2의 API 목록은 리플렉션으로 직접 확인한 것).
2. `tests/CADWorkAssistant.Integration.Tests/Ipc/AutoCadPipeClientTests.cs` — 실제 Named Pipe로 `AutoCadPipeClient`(Desktop이 쓰는 바로 그 코드)를 Fake 서버 상대로 연결/요청-응답/타임아웃/오류 코드 왕복/RequestId 보존까지 종단간 검증했다 (AutoCAD 불필요).
3. Desktop 단독 실행 시(AutoCAD 미실행) 크래시 없이 "AutoCAD Not Running"으로 정확히 표시되는 것을 UI Automation으로 직접 확인했다 (§44 Scenario 1).

**다만 실제 AutoCAD 2024 GUI를 이 PC에서 띄우는 것 자체가 불안정하다.** NETLOAD를 통한 실제 연결 스모크 테스트(§44 Scenario 2~10)를 시도하는 과정에서 AutoCAD 프로세스가 원인 불명으로 사라졌고, 그 시점에 Windows 이벤트 로그(`Application`, provider `Windows Error Reporting`)에 `LiveKernelEvent`/`BlueScreen` fault bucket이 다수 기록되어 있었다 — 그래픽 드라이버가 AutoCAD의 렌더링 부하를 못 견디고 크래시했다가 Windows가 복구한 것으로 추정된다(전체 재부팅은 없었음). 사용자 확인 결과 이 PC는 AutoCAD를 실제로 쓰는 머신이 아니며, 개발은 이 PC에서 계속하되 실제 AutoCAD 연동 검증은 AutoCAD가 정상 동작하는 별도 머신에서 진행하기로 했다.

**AutoCAD가 정상 동작하는 머신에서 다음을 확인해야 Milestone 1이 완전히 끝난 것으로 본다** (§44 시나리오 그대로):

1. Desktop 실행 → AutoCAD 미실행 상태 확인 (이미 이 PC에서 검증 완료)
2. AutoCAD 실행, Plugin NETLOAD 전 → "AutoCAD Detected · Plugin Not Loaded" 확인
3. NETLOAD 후 → 자동으로 "Connected" 전환 확인
4. DWG를 열고 → Desktop에 Drawing Name/Path/Layout/Unit이 정확히 표시되는지 확인
5. 다른 DWG로 전환 → Desktop이 최대 2초 안에 갱신되는지 확인
6. Model → Layout1 전환 → Layout 표시 갱신 확인
7. AutoCAD 종료 → Desktop 크래시 없이 Disconnected/NoAutoCadProcess로 전환되는지 확인
8. AutoCAD 재실행 + NETLOAD → Desktop이 자동으로 재연결되는지 확인
9. AutoCAD 두 개 실행 → `AvailableInstances`에 둘 다 잡히는지, `SelectInstanceAsync`로 전환되는지 확인 (현재 UI에는 선택 화면이 없어 코드로 직접 호출하거나 후속 Milestone에서 UI를 추가해야 함)
10. Unitless(단위 미지정) 도면 → "Unitless"로 정확히 표시되는지, mm로 잘못 추정하지 않는지 확인
11. AutoCAD PAN/ZOOM/PLINE 등 조작 중 Desktop의 2초 heartbeat/polling 때문에 끊김이 느껴지지 않는지 확인

## 9. 향후 AutoCAD 버전 추가

새 버전(예: AutoCAD 2026, net8.0 기반) 지원이 필요해지면:

1. `src/CADWorkAssistant.AutoCAD2026/` 프로젝트 신규 생성 (다른 TFM 가능)
2. Command/IExtensionApplication 구현을 최대한 재사용하되, 버전별 API 차이는 각 프로젝트 안에서 흡수
3. `CADWorkAssistant.Core`/`Infrastructure`는 변경 없이 공유
4. Desktop App은 연결된 AutoCAD의 버전과 무관하게 동일한 IPC 프로토콜로 통신 (프로토콜 버전 필드를 두어 하위 호환 확인)

지금은 단일 버전(2024)만 지원하며, 이 구조는 실제로 두 번째 버전을 지원해야 하는 시점에 검증한다.
