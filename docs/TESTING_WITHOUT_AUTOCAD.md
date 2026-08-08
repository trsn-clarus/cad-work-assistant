# Testing Without AutoCAD

이 프로젝트는 실제 AutoCAD가 없는 PC에서도 대부분의 기능을 개발/검증할 수 있는 3단계 테스트 구조를 쓴다 (Milestone 2 §4). 실제 AutoCAD가 없다는 것이 개발을 막는 이유가 되어서는 안 된다.

## 3단계 구조

### Level 1 — Unit Test (`tests/CADWorkAssistant.Core.Tests`)

AutoCAD가 전혀 필요 없다. `CADWorkAssistant.Core`(계산/도메인 로직)와 `CADWorkAssistant.Infrastructure`(Pipe framing 등)를 순수 함수/클래스 단위로 검증한다.

```powershell
dotnet test tests/CADWorkAssistant.Core.Tests
```

검증 대상: 단위 변환(`LengthUnitConverter`), 집계(`LengthAggregationService`), 포맷팅(`LengthFormatter`), IPC 프로토콜(`IpcRequestDispatcher`, envelope 직렬화), 연결 상태 전이(`CadConnectionStateEvaluator`), Pipe framing(`PipeMessageFramer`).

### Level 2 — Headless Integration Test (`tests/CADWorkAssistant.Integration.Tests`)

실제 AutoCAD 대신 `CADWorkAssistant.FakeAutoCad`를 **실제 별도 프로세스로** 띄우고, Desktop이 쓰는 것과 동일한 `AutoCadPipeClient`로 **실제 Named Pipe**를 통해 통신한다. AutoCAD와 100% 같은 IPC Protocol(Core.Ipc)을 쓰므로, 여기서 통과하면 프로토콜/전송 계층은 실제 AutoCAD를 붙였을 때도 그대로 동작한다고 신뢰할 수 있다.

```powershell
dotnet test tests/CADWorkAssistant.Integration.Tests
```

테스트가 FakeAutoCad 프로세스를 자동으로 시작/종료한다 (`Fixtures/FakeAutoCadProcess.cs`) - 터미널을 따로 열 필요가 없고, 테스트가 실패해도 orphan 프로세스가 남지 않는다(테스트 실행 후 `tasklist`로 직접 확인함).

### Level 3 — Real AutoCAD Validation

AutoCAD가 정상 동작하는 머신에서만 할 수 있다. 이 저장소의 개발 PC는 AutoCAD GUI를 띄우면 그래픽 드라이버가 불안정해지는 문제가 있어(Milestone 1에서 확인) 이 단계를 이 PC에서 수행하지 못한다. 확인해야 할 항목은 [`AUTOCAD_REAL_MACHINE_CHECKLIST.md`](./AUTOCAD_REAL_MACHINE_CHECKLIST.md)에 누적한다.

**이 단계가 밀렸다고 Milestone을 미완료로 처리하지 않는다** - 대신 체크리스트에 "Pending"으로 명확히 남긴다.

## CADWorkAssistant.FakeAutoCad

`tools/CADWorkAssistant.FakeAutoCad/` — 실행 가능한 콘솔 앱(net8.0). `src/`가 아니라 `tools/`에 둔 이유: 제품 코드(설치 프로그램에 들어감)가 아니라 개발자 도구이기 때문이다 (§73).

- 실제 AutoCAD Plugin과 **완전히 동일한** `IpcRequestDispatcher`/`AutoCadPipeServer`(Infrastructure.Ipc)를 그대로 재사용한다. Fake 전용 프로토콜은 없다 - Handler 구현만 AutoCAD API 대신 미리 정해둔 데이터를 반환한다.
- Pipe 이름은 실제 AutoCAD와 같은 규칙(`CADWorkAssistant.AutoCAD.{PID}`)을 그대로 쓴다 - 이 PID는 FakeAutoCad 프로세스 자신의 PID다.

### 실행

```powershell
dotnet run --project tools/CADWorkAssistant.FakeAutoCad -- --scenario NormalSelection
```

또는 빌드된 apphost를 직접 실행 (Desktop의 Discovery가 프로세스 **이름**으로 찾으므로, Simulation Mode에서 Desktop과 같이 쓸 때는 `dotnet <dll>`이 아니라 이 방식을 써야 한다 - `dotnet <dll>` 실행 시 실제 OS 프로세스 이름이 `dotnet`이 되어버려 찾을 수 없다. 실제로 겪은 문제):

```powershell
tools\CADWorkAssistant.FakeAutoCad\bin\Debug\net8.0\CADWorkAssistant.FakeAutoCad.exe --scenario NormalSelection
```

시작하면 `READY pid={pid} scenario={name}`을 stdout에 출력한다. stdin에 아무 줄이나 입력하거나 Ctrl+C로 종료한다.

### Scenario 목록

`Scenarios/ScenarioCatalog.cs`에 정의되어 있다. `GetDrawingContext`(도면 이름/Layout/단위)와 `SelectLengthObjects`(선택 결과) 양쪽에 동시에 쓰인다.

| Scenario | 검증하는 것 |
|---|---|
| `NormalSelection` (기본값) | Polyline 2개 + Line 1개, mm 단위 → 총 255.941 m (§7의 실제 예시 값) |
| `SinglePolyline` | 객체 1개 |
| `MultipleObjects` | 5개, Line/Polyline/Arc 혼합 |
| `EmptySelection` | 빈 선택 (0.000 m로 저장하면 안 됨, §43) |
| `UnsupportedObject` | 전부 제외 객체 (Hatch) |
| `MixedSupportedUnsupported` | 지원 2개 + 제외 1개 |
| `UnitlessDrawing` | Insunits 없음 → 자동 변환 금지 (§22) |
| `MetersDrawing` | 원본이 이미 m 단위 |
| `SelectionCancelled` | Esc 취소 → `IpcErrorCode.SelectionCancelled` (오류 아님, §19) |
| `ConnectionLost` | 응답 직전 프로세스 자체 종료 (AutoCAD 크래시 흉내) |
| `RequestTimeout` | 영원히 응답하지 않음 |
| `AutoCadError` | AutoCAD 내부 오류 흉내 → `ApiExecutionFailed` |
| `LargeSelection` | 객체 1,000개 (성능 테스트용, §64) |

새 Scenario는 `ScenarioCatalog.BuildAll()`에 한 항목만 추가하면 된다. Area/Layer/Plot 등 향후 기능도 같은 Scenario 시스템을 재사용할 수 있게 설계했다 (§8).

## Development Simulation Mode (Desktop)

Desktop을 실제로 띄운 채로 UI까지 확인하고 싶을 때 쓴다 (§10, §55).

```powershell
$env:CWA_USE_FAKE_AUTOCAD = "1"
dotnet run --project src/CADWorkAssistant.Desktop
```

이 환경변수가 설정되어 있으면 `AutoCadDiscoveryService`가 `acad` 대신 `CADWorkAssistant.FakeAutoCad` 프로세스를 찾는다 - 그 다음부터는 Desktop 코드에 Fake 분기가 전혀 없다. 실제 AutoCAD에 연결했을 때와 완전히 동일한 코드 경로(Discovery → ConnectionManager → Pipe → Handler)를 탄다.

연결되면 상태 표시줄에 `[SIMULATION]` 접두사가 붙는다 (`AutoCadInstanceInfo.IsSimulated`) - 사용자가 Fake 데이터를 실제 결과로 착각하지 않도록 하기 위함이다 (§39). Production Release 빌드에는 이 배지 자체가 문제되지 않는다 - `CWA_USE_FAKE_AUTOCAD`를 설정하지 않는 한 이 코드 경로는 실행되지 않고, `CADWorkAssistant.FakeAutoCad.exe`는 설치 프로그램에 포함되지 않는다.

### 실제로 검증한 것 (이 문서 작성 시점, 2026-08-08)

FakeAutoCad.exe + Desktop.exe를 실제로 별도 프로세스 두 개로 띄워서 확인함:

1. Discovery가 FakeAutoCad를 찾고 자동 연결 → `[SIMULATION] CAD Work Assistant Simulation Connected` 표시
2. `School_Roof.dwg`, `Units: mm` 등 GetDrawingContext 결과가 정확히 표시
3. "CAD에서 객체 선택" 클릭 → 실제 Named Pipe 왕복 → "3개 객체의 길이를 계산했습니다."
4. 결과 테이블: `2A7F Polyline A-WALL 125.331 m`, `2A80 Polyline A-WALL 81.405 m`, `2A81 Line A-WALL 49.204 m`
5. 총 길이 **255.941 m** (§7의 기대값과 정확히 일치)
6. "산출내역 추가" → Dashboard의 Quantity 테이블에 새 행이 정확한 값으로 추가됨

## Real Machine Test 경계

Level 2까지 커버하지 못하는 것 (Level 3에서만 확인 가능):

- 실제 AutoCAD Managed API 호출의 런타임 동작 (컴파일 성공은 확인했지만 `Editor.GetSelection`이 실제로 사용자 입력을 기다렸다가 정확한 `ObjectId`를 반환하는지는 실행해봐야 안다)
- `DocumentLock`이 실제 AutoCAD 조작(PAN/ZOOM 등)에 미치는 영향
- 실제 Arc가 포함된 Polyline의 `GetDistanceAtParameter` 결과 정확도
- 여러 AutoCAD 인스턴스를 실제로 띄웠을 때의 Discovery 동작

이 항목들은 [`AUTOCAD_REAL_MACHINE_CHECKLIST.md`](./AUTOCAD_REAL_MACHINE_CHECKLIST.md)에 정리되어 있다.
