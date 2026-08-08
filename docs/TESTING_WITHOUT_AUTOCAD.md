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

`Scenarios/ScenarioCatalog.cs`에 정의되어 있다. 하나의 `SimulationScenario`가 `GetDrawingContext`(도면 이름/Layout/단위), `SelectLengthObjects`, `SelectAreaObjects` 전부에 동시에 쓰인다 - Length와 Area는 각자 독립된 `LengthBehavior`/`Objects`와 `AreaBehavior`/`AreaObjects` 필드를 갖는다.

#### Length (13개)

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

#### Area (16개, Milestone 3)

| Scenario | 검증하는 것 |
|---|---|
| `SingleClosedPolyline` | 닫힌 Polyline 1개 |
| `MultipleClosedPolylines` | 닫힌 Polyline 3개, mm 단위 → 총 3,102.43 m² (Milestone 3 §33의 실제 예시 값) |
| `OpenPolyline` | 열린 Polyline 1개 → 0 m²가 아니라 Open으로 제외 (§16) |
| `MixedClosedOpen` | 4개 중 3개만 닫힘 → PartialSuccess, 3,102.43 m², 제외 1개 (§34) |
| `EmptyAreaSelection` | 빈 선택 |
| `AreaSelectionCancelled` | Esc 취소 |
| `UnsupportedAreaObject` | 전부 제외 객체 (Hatch) |
| `AreaMixedSupportedUnsupported` | 지원 1개 + 제외 1개 (Length의 `MixedSupportedUnsupported`와 이름이 겹쳐 `Area` 접두어를 붙였다) |
| `ZeroArea` | 닫혀 있지만 면적이 0 → `InvalidGeometry`로 제외, Valid로 합산되지 않음 (§17, §19) |
| `InvalidArea` | 닫혀 있지만 면적이 NaN(AutoCAD 예외를 흉내) → `InvalidGeometry`로 제외 (§17-18) |
| `UnitlessAreaDrawing` | Insunits 없음 → 자동 변환 금지 |
| `MeterAreaDrawing` | 원본이 이미 m 단위 |
| `AreaConnectionLost` | 응답 직전 프로세스 자체 종료 |
| `AreaRequestTimeout` | 영원히 응답하지 않음 |
| `AreaAutoCadError` | AutoCAD 내부 오류 흉내 |
| `LargeAreaSelection` | 객체 1,000개 (성능 테스트용, §35, §81) |

새 Scenario는 `ScenarioCatalog.BuildAll()`에 한 항목만 추가하면 된다.

#### Vertical Area / Parapet (Milestone 4) — 전용 Scenario 없음

Vertical Area와 Parapet은 FakeAutoCad에 새 Scenario를 추가하지 않았다. 둘 다 새 AutoCAD IPC 명령이
없고(`docs/QUANTITY_COMPOSITION.md` 참고) 기준 길이는 기존 `SelectLengthObjects`로만 들어오므로,
Integration.Tests가 기존 `NormalSelection` Scenario(255.940660 m)를 그대로 재사용해 "실제 IPC 왕복
→ Core.VerticalArea/Core.Parapet 계산"이라는 배선을 검증한다. 계산 로직 자체(32.118m 등 실무값
회귀)는 Core.Tests에서 AutoCAD/IPC 없이 이미 정밀 검증했다 - FakeAutoCad 안에는 수량 계산 로직을
넣지 않는다는 원칙을 지켰다(Milestone 4 §106: "FakeAutoCad는 AutoCAD를 Simulation하는 도구다.
공사 수량 계산은 Desktop/Core 책임이다").

## Development Simulation Mode (Desktop)

Desktop을 실제로 띄운 채로 UI까지 확인하고 싶을 때 쓴다 (§10, §55).

```powershell
$env:CWA_USE_FAKE_AUTOCAD = "1"
dotnet run --project src/CADWorkAssistant.Desktop
```

이 환경변수가 설정되어 있으면 `AutoCadDiscoveryService`가 `acad` 대신 `CADWorkAssistant.FakeAutoCad` 프로세스를 찾는다 - 그 다음부터는 Desktop 코드에 Fake 분기가 전혀 없다. 실제 AutoCAD에 연결했을 때와 완전히 동일한 코드 경로(Discovery → ConnectionManager → Pipe → Handler)를 탄다.

연결되면 상태 표시줄에 `[SIMULATION]` 접두사가 붙는다 (`AutoCadInstanceInfo.IsSimulated`) - 사용자가 Fake 데이터를 실제 결과로 착각하지 않도록 하기 위함이다 (§39). Production Release 빌드에는 이 배지 자체가 문제되지 않는다 - `CWA_USE_FAKE_AUTOCAD`를 설정하지 않는 한 이 코드 경로는 실행되지 않고, `CADWorkAssistant.FakeAutoCad.exe`는 설치 프로그램에 포함되지 않는다.

### 실제로 검증한 것

FakeAutoCad.exe + Desktop.exe를 실제로 별도 프로세스 두 개로 띄워서 확인함:

**Milestone 2 (Length)**

1. Discovery가 FakeAutoCad를 찾고 자동 연결 → `[SIMULATION] CAD Work Assistant Simulation Connected` 표시
2. `School_Roof.dwg`, `Units: mm` 등 GetDrawingContext 결과가 정확히 표시
3. "CAD에서 객체 선택" 클릭 → 실제 Named Pipe 왕복 → "3개 객체의 길이를 계산했습니다."
4. 결과 테이블: `2A7F Polyline A-WALL 125.331 m`, `2A80 Polyline A-WALL 81.405 m`, `2A81 Line A-WALL 49.204 m`
5. 총 길이 **255.941 m** (§7의 기대값과 정확히 일치)
6. "산출내역 추가" → Dashboard의 Quantity 테이블에 새 행이 정확한 값으로 추가됨

**Milestone 3 (Area, 2026-08-08)**

1. `MixedClosedOpen` Scenario로 "CAD에서 영역 선택" 클릭 → "4개 중 3개 영역의 면적을 계산했습니다.", 제외 배너 "선택한 4개 객체 중 1개는 면적 계산에서 제외했습니다 (열린 형상 1개)." 정확히 표시
2. 결과 테이블: 3개 행(`7020`/`7021`/`7022` Polyline A-FLOOR, 각 1,520.42 m²/981.27 m²/600.74 m²), 열린 항목은 행으로 들어가지 않음
3. 총 면적 **3,102.43 m²** (§33의 기대값과 정확히 일치)
4. "산출내역 추가" → Dashboard Quantity 테이블에 `Area / 3,102.430 / m²` 행 추가 확인
5. `AreaAutoCadError` Scenario → "면적을 계산하지 못했습니다.\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요." (Error 상태, 빨간 표시)
6. `UnitlessAreaDrawing` Scenario → "도면 단위가 설정되어 있지 않습니다.", 행은 `500,000.00 (Unitless)`로 원본값 그대로 표시, "산출내역 추가" 버튼 비활성화 확인
7. `EmptyAreaSelection` Scenario → "선택된 객체가 없습니다.", "산출내역 추가" 버튼 비활성화 확인

이 과정에서 실제 버그 하나를 발견/수정했다: `InvalidArea` Scenario(닫혀 있지만 면적이 `double.NaN`)로 Integration Test를 돌렸을 때 `NullReferenceException`이 발생했다 - `System.Text.Json`이 기본 설정으로는 `NaN`을 직렬화하지 못해 IPC 응답이 깨졌기 때문이다. `IpcJson.Options`에 `JsonNumberHandling.AllowNamedFloatingPointLiterals`를 추가해 해결했다 (Length를 포함한 IPC 계층 전체에 적용되는 수정이라 회귀 테스트를 `Core.Tests/Ipc/IpcEnvelopeTests.cs`에 추가했다).

**Milestone 4 (Vertical Area / Parapet, 2026-08-08)**

1. `NormalSelection` Scenario로 Vertical Area → "CAD에서 기준선 선택" 클릭 → "기준 길이 255.941 m" 표시
2. 높이 TextBox에 `0.1` 입력(버튼 없이 `UpdateSourceTrigger=PropertyChanged`로 즉시 재계산) → "255.941 m × 0.100 m" / "25.594 m²" 실시간 표시 확인
3. 높이를 `0`으로 바꾸면 "높이는 0보다 커야 합니다." + "산출내역 추가" 버튼 비활성화, 다시 `0.1`로 되돌리면 즉시 복구
4. "산출내역 추가" → Dashboard Quantity 테이블에 `VerticalArea / 25.594 / m²` 행 추가 확인
5. Parapet에서 같은 방식으로 둘레 선택 → 높이 `1.0` → "양면" 라디오 선택(양면 511.881 m² = 255.941×1.0×2 확인) → "상부면 포함" 체크 → 폭 `150`(mm) 입력 → 측면 511.881 m² + 상부 38.391 m² = 총 550.272 m² 정확히 표시, "산출내역 추가" → Dashboard에 `Parapet / 550.272 / m²` 확인
6. 직접 입력(Manual) 소스로 전환 → `32118`(mm) 입력 → "32.118 m × 1.000 m" = "32.118 m²" 정확히 계산 (§57의 실무값과 별개로 단위 변환 자체가 UI에서 올바르게 동작하는지 확인)

이 과정에서 실제 버그 하나를 더 발견/수정했다: Length 도구에서 새 측정을 완료한 뒤 Vertical Area로
전환해 "최근 측정값 사용" 라디오를 선택하면 계속 비활성화 상태로 남아 있었다 - `LengthWorkflowViewModel.LastResult`가
값은 정확히 갱신되고 있었지만 `OnPropertyChanged(nameof(LastResult))`를 호출하지 않아 WPF 바인딩이
그 변경을 몰랐기 때문이다(계산 자체는 항상 맞았고, 데이터 바인딩 알림 누락이었다). `LengthWorkflowViewModel`에
알림 호출을 추가하고 `LengthSourceSelector`가 그 알림을 구독하도록 고쳤다 - Core.Tests/Integration.Tests는
WPF 바인딩을 거치지 않아 이 버그를 잡아내지 못했고, Simulation Mode 수동 검증에서만 발견됐다 - "UI까지
실제로 띄워 확인한다"는 이 문서의 존재 이유를 그대로 보여준 사례다.

## Real Machine Test 경계

Level 2까지 커버하지 못하는 것 (Level 3에서만 확인 가능):

- 실제 AutoCAD Managed API 호출의 런타임 동작 (컴파일 성공은 확인했지만 `Editor.GetSelection`이 실제로 사용자 입력을 기다렸다가 정확한 `ObjectId`를 반환하는지는 실행해봐야 안다)
- `DocumentLock`이 실제 AutoCAD 조작(PAN/ZOOM 등)에 미치는 영향
- 실제 Arc가 포함된 Polyline의 `GetDistanceAtParameter` 결과 정확도
- 여러 AutoCAD 인스턴스를 실제로 띄웠을 때의 Discovery 동작
- 실제 `Curve.Area`/`Region.Area`의 런타임 동작 - 특히 자기교차 Polyline이나 매우 복잡한 형상에서
  값을 정상 반환하는지, 아니면 `Autodesk.AutoCAD.Runtime.Exception`을 던지는지는 실물로 확인해야 한다
  (Milestone 3 §18)
- `Polyline3d`/`Hatch`를 이번 Milestone에서 의도적으로 Unsupported 처리했는데, 실제 도면에서 사용자가
  얼마나 자주 이런 객체를 면적 계산에 섞어 선택하는지는 실사용 피드백이 필요하다

이 항목들은 [`AUTOCAD_REAL_MACHINE_CHECKLIST.md`](./AUTOCAD_REAL_MACHINE_CHECKLIST.md)에 정리되어 있다.
