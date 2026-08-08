# Drawing Navigation (Milestone 5)

Length/Area/Vertical Area/Parapet(Milestone 2-4)는 "선택 → 계산 → 저장" 흐름이었다. 이 Milestone은
성격이 다르다 - 계산값을 만드는 게 아니라, 복잡한 DWG 안에서 필요한 부분을 찾고, 보고, 분리하고,
안전하게 별도 파일로 추출하는 것이 목적이다. 이 문서는 그 설계와, 어디까지 Headless로 검증했고
어디부터 실제 AutoCAD가 필요한지를 기록한다.

## 전체 구조

```
DrawingWorkflowViewModel (Navigation + Selection, 하나로 합침 - §80)
  ├── SelectionSession (Core.Drawing) - 한 번의 선택 결과. Zoom/Isolate/Export가 전부 이걸 재사용한다
  ├── LayerWorkflowViewModel (Layers) - 조회/검색/개별 토글/"선택 Layer만 보기"
  └── ExportWorkflowViewModel (Export) - 설명 → 파일명 제안 → SaveFileDialog → ExportSelection IPC
```

Selection과 Navigation(Zoom)을 분리하지 않은 이유(§80): 같은 SelectionSession을 두고 "화면에
맞추기"와 "격리하기"라는 서로 다른 동작을 트리거할 뿐이라, 나누면 오히려 상태를 두 ViewModel에
나눠 가지면서 동기화 문제만 생긴다. Layer/Export는 책임이 명확히 달라 분리했다(§81-82).

## IPC 명령 (9개)

Milestone 5 §11의 13개 후보를 아래처럼 통합했다 - "지나치게 세분화하거나 거대한 하나로 만들지
않는다"는 원칙에 따라, 의미상 하나의 트랜잭션인 것들을 합쳤다.

| 명령 | 통합한 후보 | 비고 |
| --- | --- | --- |
| `GetDrawingOverview` | - | Extents/객체수/Layer수 |
| `ZoomExtents` | - | |
| `ZoomToBounds` | `ZoomSelection` | Bounds 출처를 가리지 않는 범용 명령으로 - Selection이 아닌 다른 Bounds도 나중에 재사용 가능 |
| `SelectDrawingObjects` | `SelectObjects` + `GetSelectedObjects` | 선택 즉시 전체 데이터를 응답하므로 별도 조회 명령이 필요 없다 |
| `IsolateObjects` | `HideObjects` | "선택만 보기"가 핵심 요구였고(§32), "숨기기"만 별도로 구현할 근거가 약해 이번엔 만들지 않았다(§137 최우선 원칙에 따라 필요한 만큼만) |
| `GetLayers` | - | |
| `SetLayerVisibility` | `IsolateLayers` | 개별 토글과 "선택 Layer만 보기"는 "이 Layer들을 이 상태로" 라는 같은 모양의 요청이라 하나로 통일 |
| `RestoreVisibility` | `RestoreLayers` | Object Isolation과 Layer Isolation을 사용자는 구분해서 인지하지 않는다(둘 다 "복원" 버튼 하나) - 서버도 하나의 복원 요청으로 둘 다 되돌린다 |
| `ExportSelection` | - | |

`ExportSelectionFileExists`(파일 존재 확인)는 IPC 명령으로 만들지 않았다 - `SaveFileDialog`의
`OverwritePrompt`가 이미 처리하므로(§57), AutoCAD Handler는 "이 경로에 써라"만 알면 된다.

## SelectionSession과 Bounds (§13-17, §66-67)

`Core.Drawing.SelectionSession`이 한 번의 선택 결과(Handle 목록·타입별/Layer별 집계·합산 Bounds·
생성 시각)를 담는다. `BoundsAggregator.Aggregate`는 여러 `CadBoundsDto`의 union을 계산하고,
`NaN`/`Infinity`가 섞인 잘못된 Extents(빈 Block 등)를 걸러낸다 - AutoCAD 비의존, Core에서 27개
케이스로 테스트했다(단일/다중/음수좌표/대형좌표/무효 Extents/빈 목록).

## Isolation/Restore 설계 (§32-48) — 가장 중요한 부분

### Object Isolation

AutoCAD Managed API에는 네이티브 "Isolate Objects" 기능(우클릭 메뉴)에 대응하는 공개 API가 없다
(리플렉션으로 `Isolate`/`Hide` 이름의 타입을 찾아봤지만 없었다 - 추측 대신 확인). 대신 표준적으로
쓰이는 `Entity.Visible` 프로퍼티를 직접 토글한다:

- `IsolateObjectsHandler`가 ModelSpace를 순회해, 선택되지 않았고 *지금 실제로 보이는* Entity만
  `Visible = false`로 바꾸고 그 Handle을 `DrawingIsolationState.HiddenObjectHandles`에 기록한다.
- `RestoreVisibilityHandler`는 정확히 그 Handle들만 `Visible = true`로 되돌린다.
- **알아둬야 할 특성**: `Entity.Visible`은 Database에 저장되는 진짜 프로퍼티라(DXF group code 60),
  트랜잭션을 Commit해야 화면에 반영된다 - Commit하지 않으면(읽기 전용 패턴처럼 Abort하면) 아무
  일도 일어나지 않는다. 즉 이 변경은 AutoCAD의 "수정됨" 상태/Undo 스택에 흔적을 남길 가능성이
  높다. 원본 파일을 임의로 저장하지 않는다는 절대 원칙(CLAUDE.md #1)은 지키지만, "수정됨" 표시
  자체가 뜨는지는 실제 AutoCAD에서만 확인 가능하다(`AUTOCAD_REAL_MACHINE_CHECKLIST.md`).

### Layer Isolation/토글

`SetLayerVisibilityHandler`는 **이번 세션에서 처음 상태를 바꾸는 순간에만** 전체 LayerTable의
On/Off 상태를 스냅샷으로 남긴다(`DrawingIsolationState.OriginalLayerOnState`). 이후 몇 번을
토글하든 스냅샷은 갱신하지 않는다 - 그래야 `RestoreVisibility`가 "지금 상태"가 아니라 "맨 처음
상태"로 돌아갈 수 있다. 이게 §46의 핵심 원칙이다:

```
잘못된 복원: Restore → 모든 Layer On
올바른 복원: Restore → 작업 전 Layer 상태 (원래 Off였던 Layer는 계속 Off)
```

Headless E2E(`LayerIsolationEndToEndTests.IsolateByLayer_ThenRestore_ReturnsExactOriginalState_NotAllOn`)가
정확히 이 시나리오를 검증한다 - Fake 시나리오의 A-DOOR Layer는 원래 Off로 시작하고, "A-WALL만
보기"로 전부 재배치한 뒤 Restore하면 A-DOOR가 다시 Off로 돌아오는지 확인한다. 이 테스트가 실패하면
"전부 On으로 복원"하는 버그가 생겼다는 뜻이다.

### 현재 Layer 보호 (§44)

`SetLayerVisibilityHandler`/`SetLayerVisibilityRequest`가 현재 활성 Layer(`Database.Clayer`)를 Off로
바꾸는 요청만 조용히 무시한다(On으로 바꾸는 요청은 그대로 적용 - 이미 켜져 있어 no-op). Desktop의
`LayerRow.IsOn` setter도 같은 규칙을 클라이언트에서 미리 확인한다 - 서버가 "성공"으로 응답해도
실제로는 아무것도 안 바뀐 경우(현재 Layer Off 시도)를 체크박스가 거짓으로 보여주지 않기 위해서다
(Simulation Mode에서 실제로 겪은 버그, 아래 "실제로 겪은 버그" 참고).

### Freeze/Thaw는 조회만

§42의 권고대로 Read/Restore 정확성이 충분히 보장되지 않아 Freeze/Thaw 토글은 만들지 않았다.
`CadLayerDto.IsFrozen`은 표시 전용이다.

## WBLOCK Export (§49-63)

```csharp
using var outputDatabase = sourceDatabase.Wblock(objectIdCollection, Point3d.Origin);
outputDatabase.SaveAs(targetFilePath, DwgVersion.Current);
```

`Database.Wblock(ObjectIdCollection, Point3d)`는 원본 Database를 전혀 건드리지 않고 완전히 새
in-memory Database를 반환한다 - Layer/Linetype/TextStyle/Block 정의 등 Dependency는 Wblock이
자동으로 함께 담아온다(AutoCAD의 표준 동작으로 알려져 있으나, 정확성은 Real AutoCAD에서만 최종
확인 가능하다 - `AUTOCAD_REAL_MACHINE_CHECKLIST.md` WBLOCK 섹션 참고). 원본 파일명/저장 경로/
덮어쓰기 확인은 전부 Desktop의 native `SaveFileDialog`가 처리한다(§57) - AutoCAD Handler는 "이
Handle들을 이 경로에 WBLOCK으로 저장"만 안다. 파일명 제안(`ExportFileNameService.SuggestFileName`)과
살균(`Sanitize`)은 AutoCAD/파일시스템 비의존 순수 로직이라 Core에 있다.

## Zoom (§21-25)

`_ZOOM _E` 같은 명령 문자열 대신 Managed API로 View를 직접 계산한다(§22). 표준적으로 알려진
"WCS→DCS 변환 후 Extents를 그 좌표계에서 재계산" 기법을 따랐다:

```
worldToDcs = Matrix3d.PlaneToWorld(view.ViewDirection)
worldToDcs = Matrix3d.Displacement(view.Target - Point3d.Origin) * worldToDcs
worldToDcs = Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) * worldToDcs
worldToDcs = worldToDcs.Inverse()
```

Bounds의 8개 꼭짓점을 이 행렬로 변환해 DCS 공간에서 min/max를 구하고, 10% 여백(§24)을 적용해
`ViewTableRecord.Width`/`Height`/`CenterPoint`에 대입한 뒤 `Editor.SetCurrentView`로 반영한다.
ViewDirection/ViewTwist/Target이 전부 기본값이 아닌 임의의 3D 뷰에서도 맞도록 일반화했지만, 실제
사용 시나리오(정면에서 본 2D 평면도)로만 우선 검증 가능하다 - **Zoom은 이 Milestone에서 실제
AutoCAD 화면 없이 전혀 시각적으로 확인하지 못한 유일한 핵심 기능**이다.

## Headless로 검증한 것 / Real AutoCAD가 필요한 것

| 검증 대상 | Headless (FakeAutoCad + Integration.Tests) | Real AutoCAD 필요 |
| --- | --- | --- |
| IPC 계약/직렬화/RequestId/에러 코드 | ✅ | |
| Selection Cancel/Timeout/Disconnect/Error | ✅ | |
| Bounds 집계/타입별·Layer별 집계 (Core) | ✅ | |
| Layer Restore가 "전부 On"이 아니라 정확한 원래 상태인지 | ✅ (Fake의 mutable 상태로) | 실제 LayerTable에서도 동일한지 |
| 현재 Layer 보호 | ✅ | |
| 파일명 제안/살균 (Core) | ✅ | |
| Export 시 파일이 대상 경로에 실제로 생성되는지 | ✅ (placeholder 파일) | **WBLOCK 결과물의 정확성/완전성** |
| Zoom View 계산이 실제로 맞는 곳을 보여주는지 | ❌ (Fake는 View가 없다) | ✅ 전부 |
| Window/Crossing 선택의 실제 기하학적 판정 | ❌ | ✅ 전부 |
| Entity.Visible 변경의 Undo/수정 플래그 영향 | ❌ | ✅ 전부 |
| Xref/Dimension/Hatch/중첩 Block Export 정확성 | ❌ | ✅ 전부 |

## Simulation Mode 시각 검증 중 실제로 겪은 버그 2건

두 버그 모두 코드 리뷰만으로는 발견하지 못했다 - 실제로 Desktop을 띄우고 화면을 캡처해서 발견했다.

1. **`OnActivated()`의 IsBusy 경쟁 상태**: `DrawingWorkflowViewModel.OnActivated()`가 처음에는
   `CanRunCommand()`(`!IsBusy && Connected`)로 Overview/Layer 로드 여부를 각각 판단했는데,
   `RefreshOverview()`가 첫 `await` 이전에 동기적으로 `IsBusy = true`를 설정하는 바람에(async void의
   동기 구간), 바로 다음 줄의 Layer 로드 조건이 항상 `false`가 되어 Layer Manager가 탭을 열어도
   절대 채워지지 않았다. 연결 상태만 직접 확인하도록 고쳤다(`IsBusy`와 무관하게 만든다).
2. **`DataGridCheckBoxColumn`의 기본 TwoWay 바인딩이 읽기 전용 속성에서 예외**: `IsFrozen`/`IsLocked`는
   getter만 있는 속성인데, `DataGridCheckBoxColumn`은 `IsReadOnly="True"`(셀 편집 금지)를 줘도
   내부 `Binding`의 기본 Mode는 여전히 TwoWay다. 바인딩 활성화 시점에
   `InvalidOperationException`이 던져졌고, 이 예외가 레이아웃 패스 도중 발생해 Layer Manager
   DataGrid 전체가 헤더만 보이고 행이 하나도 렌더링되지 않는 상태로 남았다(로그의 "Unhandled UI
   exception"으로 확인). `Binding="{Binding IsFrozen, Mode=OneWay}"`로 명시해서 해결했다 -
   **읽기 전용 CLR 속성을 `DataGridCheckBoxColumn`/`DataGridTextColumn`에 바인딩할 때는 항상
   `Mode=OneWay`를 명시한다**는 게 이번에 얻은 교훈이다.

## 의도적으로 하지 않은 것

- Freeze/Thaw 토글 (조회만)
- 객체 숨기기(선택 안 한 것만 끄기, isolate와 별개인 hide) - isolate가 핵심 요구를 이미 충족
- Zoom Window(사용자가 두 점을 지정해 확대) - AutoCAD 자체 기능과 중복도가 높아 보류(§25)
- Polygon Selection - Window/Crossing으로 핵심 요구는 충족, `SelectWindowPolygon`/`SelectCrossingPolygon`
  존재는 리플렉션으로 확인했으나 이번엔 UI까지 만들지 않았다
- Xref 특수 처리 - 실제 동작을 Real AutoCAD에서 관찰한 뒤 재평가
- Drawing Cluster 자동 탐지(여러 도면이 한 Model Space에 흩어져 있을 때 자동으로 구역을 나누는 것,
  §65) - 이번 Milestone은 그 기반(Zoom/Selection/Bounds)만 만들었다
