# CAD Text Tools (Milestone 12)

## 1. 목표

DBText/MText를 선택 → 조회 → 값만 바꿔 일괄 수정하거나, AutoCAD에서 지정한 위치에 새로 작성하는
기능. `TEXT`/`MTEXT` 명령을 다시 구현하는 것이 목표가 아니라, "높이를 어떻게 바꾸지", "색상을
ByLayer로 되돌리려면", "높이가 제각각인 문자들을 한 번에 통일하려면" 같은 반복 작업을 안전하게
자동화하는 것이 목표다. 선택하지 않은 객체/속성은 절대 건드리지 않고, 배치(batch) 수정은 가능한 한
원자적이며(부분 실패 상태 없음), Create/Edit는 각각 하나의 깨끗한 Undo 단위가 되어야 한다.

## 2. Milestone 12A / 12B 분리

Milestone 8.5(Plugin 실사용), 11B(실제 Plot 정확성)와 같은 판단이다.

- **12A(이번 범위, 완료)**: 도메인 모델/IPC 계약/AutoCAD Handler(실제 2024 API를 리플렉션으로 전량
  검증)/FakeAutoCad/Desktop UI/Headless E2E까지 실제 AutoCAD GUI 없이 전부 구현하고 Simulation
  Mode로 종단간 검증했다.
- **12B(BLOCKED)**: 실제 DBText/MText 렌더링, 폰트/TextStyle 실제 동작, 실제 화면상 문자 높이/회전/
  정렬 시각 확인, ByLayer/명시 색상 실제 표시, 실제 Undo/Redo 스택 동작, DBMOD/저장 확인 프롬프트
  동작, 잠긴 Layer 실제 동작, Annotative 문자 실제 동작 — 전부 실제 AutoCAD 2024 GUI가 필요하다.
  이 개발 PC는 AutoCAD GUI 구동 시 그래픽 드라이버가 불안정해지는 문제가 해소되지 않아(Milestone 1,
  `docs/AUTOCAD_INTEGRATION.md` §8) BLOCKED로 남긴다. 세부 체크리스트는
  `docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md` "Milestone 12" 참고.

## 3. 지원 범위

- **지원**: `DBText`(단일행 문자), `MText`(여러행 문자).
- **명시적으로 제외**: `Dimension`, `MLeader`, `Table`, `AttributeReference` — 이들은 "문자를 담고
  있는" 객체이긴 하지만 이 기능이 다루는 순수 텍스트 객체가 아니다. 선택 시 자동 제외되고, 제외된
  타입 이름을 사용자에게 보여준다("지원되지 않는 문자 객체", Length/Area의 기존 제외-타입-안내
  패턴을 그대로 재사용).
- **범위 밖(v1에서 의도적으로 하지 않음)**: OCR, AI/LLM 기반 기능, 번역, 맞춤법 검사, Dimension
  텍스트 override, Table/Attribute 편집, Field 표현식, MText 서식 편집기(Bold/Italic/색상 부분
  적용 등), TextStyle 관리자, 폰트 설치, Annotative Scale 관리자, 프로젝트 전체 찾기/바꾸기.
  회전(Rotation)/TextStyle은 조회만 가능하고 편집 기능은 제공하지 않는다(복잡도 대비 실사용
  빈도가 낮다고 판단).

## 4. 아키텍처

```
Desktop.ViewModels.TextWorkflowViewModel (+ 합성 TextCreateViewModel)
  ▼ (선택) SelectTextObjects IPC (인터랙티브, Editor.GetSelection)
  ▼ (편집) UpdateTextObjects IPC — Content(단일 선택만)/Height/Color/Layer 부분 patch
  ▼ (작성) AcquireTextInsertionPoint IPC(인터랙티브, Editor.GetPoint) → CreateText IPC
  ▼
AutoCAD.Ipc.Handlers.{SelectTextObjectsHandler,AcquireTextInsertionPointHandler,
                       UpdateTextObjectsHandler,CreateTextHandler}
  │ AutoCadTextEntityAdapter(DBText/MText 공통 읽기/쓰기, 유일한 접점)
  ▼
DBText/MText 실제 엔티티 (하나의 Transaction, 한 번만 Commit = 하나의 Undo 단계)
```

## 5. Core 도메인 모델 (`CADWorkAssistant.Core.Text`, AutoCAD 비의존)

- `CadTextEntityType`(SingleLine/MultiLine) + `CadTextEntityTypeDisplay`(한국어 라벨)
- `CadColorMode`(ByLayer/ByBlock/Aci/TrueColor), `CadColorDto`(값 동등성 직접 구현 — Core가
  netstandard2.0이라 `System.HashCode`가 없어 수동 `unchecked` 콤바이너 사용), `CadColorPalette`
  (ByLayer/ByBlock + 7색 `CommonAci`, 색상 7은 "White/Black" 표기 — AutoCAD의 색상 7 = 배경에 따라
  흰색/검은색으로 다르게 보이는 문제를 실제로 반영)
- `CadTextObjectDto` — Handle/EntityType/Content(원본, 서식 포함 가능)/PlainText(순수 텍스트)/
  LayerName/Height/Rotation/Color/TextStyleName/IsLocked/IsAnnotative/HasInlineFormatting.
  의도적으로 Position 필드가 없다 — 이번 Milestone의 어떤 화면도 기존 객체의 위치를 표시/편집하지
  않는다(Create만 새 위치를 받는다, `CadPointDto`로 별도 처리) — "실제 필요한 필드만" 원칙.
- `OptionalValue<T>` — `HasValue`/`Value` 명시적 패턴. `null`이 "바꾸지 않음"인지 "지운다"인지
  모호해지는 문제를 없앤다.
- `TextUpdatePatch` — Content/Height/LayerName/Color 각각 `OptionalValue<T>`, `HasAnyChange` 계산
  프로퍼티. "체크한 항목만 바뀐다"는 요구사항을 타입으로 강제한다.
- `BatchPropertyKind`(Empty/Uniform/Mixed) + `BatchPropertyState<T>` + 제네릭
  `BatchPropertyAggregator.Aggregate<T>(...)` — 여러 선택 객체의 값이 "전부 같음/제각각"인지를
  문자열 `"혼합"`으로 뭉개지 않고 구조화된 타입으로 표현, Height/Layer/Color 셋 다 같은 함수 재사용.
- `TextHeightValidator`(0보다 크고 NaN/Infinity 아님), `TextContentValidator`(공백만은 무효, trim은
  하지 않음 — 사용자가 의도한 앞뒤 공백을 임의로 지우지 않는다).

## 6. IPC 계약 (`Core.Ipc.IpcMessageTypes`, 새 Protocol Version 불필요)

`SelectTextObjects`(인터랙티브 선택) / `AcquireTextInsertionPoint`(인터랙티브 단일 점 획득,
Milestone 11 `AcquirePlotWindow`와 같은 패턴이나 두 점이 아닌 한 점) / `CreateText` / `UpdateTextObjects`.

## 7. AutoCAD Plugin 구현 — 실제 API, 리플렉션으로 전량 검증

이 PC의 실제 AutoCAD 2024 `acdbmgd.dll`을 리플렉션으로 확인한 핵심 사항(추측 없음):

```
Autodesk.AutoCAD.DatabaseServices.DBText - Entity 파생, 파라미터 없는 생성자 있음
  Point3d Position / double Height / double Rotation / string TextString
  ObjectId TextStyleId / Justify(TextHorizontalMode/TextVerticalMode) / double WidthFactor

Autodesk.AutoCAD.DatabaseServices.MText - Entity 파생, 파라미터 없는 생성자 있음
  Point3d Location / double TextHeight / double Rotation
  string Contents      // 원본, 서식 코드(\P, {\C1;...} 등) 포함 가능
  string Text          // 읽기 전용, 서식이 제거된 순수 텍스트
  string ContentsRTF / ObjectId TextStyleId / double Width / Attachment

Autodesk.AutoCAD.Colors.Color (Autodesk.AutoCAD.Colors.dll)
  static Color FromColorIndex(ColorMethod, Int16) / FromRgb(byte,byte,byte) / FromEntityColor(...)
  bool IsByLayer / IsByBlock / IsByAci / IsByColor
  Int16 ColorIndex / byte Red,Green,Blue / string ColorNameForDisplay / ColorMethod ColorMethod

Entity(공통)
  Colors.Color Color (get/set) / int ColorIndex(get/set, 레거시 호환 별도 프로퍼티)
  string Layer(get/set) / ObjectId LayerId(get) / AnnotativeStates Annotative(get, 조회 전용)

Database
  ObjectId Textstyle / ObjectId TextStyleTableId / ObjectId CurrentSpaceId(현재 Model/Paper Space)
  ObjectId Clayer(현재 Layer)

LayerTableRecord.IsLocked (bool)
TransactionManager.AddNewlyCreatedDBObject(DBObject, bool) — AppendEntity 직후 필수
BlockTableRecord.AppendEntity(Entity) → ObjectId
```

실제로 확인/정정한 것들:

- **`Editor`에 Undo를 표시적으로 시작/종료하는 API가 없다** — `Autodesk.AutoCAD.EditorInput.Editor`
  전체를 "Undo"로 검색해도 0건. `TransactionManager`에도 "여러 작업을 하나의 Undo로 묶는" 전용
  API가 없다(`Database.UndoRecording`/`DisableUndoRecording(bool)`은 기록 자체를 껐다 켰다 할 뿐,
  그룹핑 기능이 아니다). 표준 Managed API 동작은 **하나의 `Transaction`을 한 번만 Commit하면 그
  자체가 하나의 Undo 단계가 된다** — 별도 Undo Mark 호출이 필요하지도, 존재하지도 않는다. 이게
  `CreateTextHandler`/`UpdateTextObjectsHandler` 양쪽의 아키텍처 근거다.
- `MText.Text`는 읽기 전용 — 서식이 제거된 순수 텍스트를 얻는 유일한 공식 경로. 서식 유지 여부
  판단(§8)에 그대로 쓴다.
- `Entity.Annotative`는 `AnnotativeStates`(True/False/NotApplicable) — 조회에 이 열거형을 그대로
  쓴다(v1은 조회만, §3).

구현 방침:

- **`AutoCadTextEntityAdapter`**(내부 static 클래스)가 DBText/MText 공통 읽기(`BuildDto`)/쓰기
  (`ApplyPatch`)/색상 변환의 유일한 접점이다 — 두 타입의 서로 다른 프로퍼티 이름(`TextString`/
  `Height` vs `Contents`/`TextHeight`)을 여기서만 흡수한다.
- **MText 서식 보존**: `Contents`(원본)와 `Text`(순수 텍스트)를 비교해 서식이 있는지만 감지하고
  (`HasInlineFormatting`), 커스텀 서식 파서를 절대 만들지 않는다(§46) — 이 기능은 서식을 편집하지
  않으므로 굳이 이해할 필요가 없다.
- **`UpdateTextObjectsHandler`**: 2단계 — (1) 전체 handle 유효성 + 현재 Layer/대상 Layer Lock
  여부를 먼저 전부 검증하고 하나라도 실패하면 실제 쓰기를 시작조차 하지 않는다(all-or-nothing), (2)
  전부 통과해야 실제로 `UpgradeOpen`+`ApplyPatch`를 순회하고 마지막에 `Transaction.Commit()`을 한
  번만 호출한다. 쓰기 도중 예외가 나도 Commit 전이므로 `using`이 Transaction을 Abort해 부분 수정이
  남지 않는다.
- **`CreateTextHandler`**: 대상 Layer(명시 또는 `Database.Clayer`)가 잠겨 있지 않은지 확인 →
  `DBText`/`MText`를 `Database.CurrentSpaceId`(Model 또는 Paper Space, 하드코딩하지 않음)의
  BlockTableRecord에 Append → `AddNewlyCreatedDBObject` → 응답 DTO를 **Commit 전에** 만든다
  (Milestone 11 `PlotDrawingPdfHandler`와 같은 순서 — Commit 후에는 Transaction이 끝나 안전하게
  조회할 수 없다). 실패 시(색상 지정 단계 등 Append 전) 아직 Database에 속하지 않은 엔티티는 직접
  `Dispose()`한다 — Append 이후 실패라면 Transaction Abort가 이미 처리하므로 이중으로 Dispose하지
  않는다(`appended` 플래그로 구분).
- **보안(§48-49, §103)**: 문자 내용을 AutoCAD에 명령 문자열로 보내지 않는다 — `SendStringToExecute`
  를 전혀 쓰지 않고, Managed API 프로퍼티 대입(`TextString =`/`Contents =`)만 사용한다.

## 8. 오류 분류 — `InvalidRequest` vs `ApiExecutionFailed`

Desktop이 서버 메시지를 그대로 사용자에게 보여줄 수 있는지 여부를 이 분류로 결정한다(CLAUDE.md
절대 원칙 4, "원시 Exception 노출 금지"):

- **`InvalidRequest`**: 이 요청을 현재 상태로는 처리할 수 없다는, 사람이 만든 안전한 한국어
  메시지("일부 문자 객체를 찾을 수 없습니다", "'A-LOCKED' Layer가 잠겨 있어 이 Layer의 문자는
  수정할 수 없습니다", "Layer 'X'를 찾을 수 없습니다"). Desktop의 `DescribeError`가 이 코드일 때만
  `error.Message`를 그대로 표시한다.
- **`ApiExecutionFailed`**: `catch (Autodesk.AutoCAD.Runtime.Exception ex)`에서 잡은 raw 예외
  메시지(`ex.Message`) — 기술적이고 예측 불가능한 내용일 수 있어 Desktop은 이 코드에서는 항상
  일반화된 안내 문구로 대체한다.

잠긴 Layer 실패는 처음에 `ApiExecutionFailed`로 분류했다가(예외와 같은 취급), Simulation Mode로
실제 화면에서 확인하는 과정에서 "AutoCAD 연결 상태를 확인해주세요"라는 엉뚱한 일반 메시지가
뜨는 것을 발견해 `InvalidRequest`로 재분류했다 — invalid-handle과 본질적으로 같은 종류(현재 상태
때문에 처리할 수 없는 요청)이므로 같은 분류가 맞다. Real Handler와 Fake Handler 양쪽, Desktop
`DescribeError`(TextWorkflowViewModel/TextCreateViewModel), Integration Test 기대값을 모두
일관되게 맞췄다.

## 9. FakeAutoCad — 실제 DWG 편집을 절대 흉내내지 않는다

`FakeCreateTextHandler`/`FakeUpdateTextObjectsHandler`는 IPC/검증/오류 처리/UI 배관만 검증하고,
실제 AutoCAD 엔티티 생성/렌더링을 절대 주장하지 않는다(§95). 단, **Batch Patch 의미론**은 실제로
검증 가능해야 하므로 `FakeUpdateTextObjectsHandler`는 `SimulationScenario.TextObjects`의 handle이
일치하는 항목에 실제로 patch를 적용해(값만 바뀐 새 DTO) 돌려준다 — Named Pipe를 통해 진짜로
"체크한 속성만 바뀌고 나머지는 그대로인지"를 종단간 확인할 수 있다.

Scenario 16개: `TextSelectionNormal`/`Mixed`/`Unsupported`(Dimension/MLeader 제외 확인)/`Cancelled`,
`TextUpdateSingle`/`TextBatchHeight`/`TextBatchByLayer`/`TextBatchLayer`/`TextBatchMixedProperties`/
`TextUpdateInvalidHandle`/`TextUpdateLocked`/`TextUpdateError`, `TextCreateDbText`/`TextCreateMText`/
`TextCreateCancelled`, `TextDisconnected`.

## 10. Desktop UI — CAD > Text

`TextWorkflowViewModel`이 Length/Area/Drawing과 같은 패턴(Coordinator 계층 없이 직접
`connectionManager.SendRequestAsync` 호출 — Plot과 달리 Persistence 기록이 없어 필요 없다, §11).
"편집/작성" 세그먼트 토글 하나로 한 화면 안에서 전환한다(별도 페이지 두 개가 아님).

- **편집 모드**: DataGrid(TYPE/CONTENT/LAYER/HEIGHT/COLOR) + 우측 배치 편집 패널. Height/Color/
  Layer 각각 체크박스로 게이팅된 입력 컨트롤("현재: {값 또는 혼합}" 캡션으로 `BatchPropertyState`
  요약 표시) — 체크하지 않은 항목은 절대 바뀌지 않는다(§67, 자유 드롭다운보다 안전). Content 편집은
  단일 선택일 때만 가능(§19, 다중 선택에서 실수로 전부 같은 내용으로 덮어쓰는 것을 방지).
- **작성 모드**(`TextCreateViewModel`): 단일행/여러행 라디오, 내용, 높이, Layer(현재 도면의 Layer
  목록 재사용, 기본값 = 현재 Layer), 색상(ByLayer/ByBlock + 7색 팔레트, 기본 ByLayer). "CAD에서
  위치 지정 후 작성" 버튼 하나가 `AcquireTextInsertionPoint`→`CreateText` 두 단계를 순서대로
  수행한다("위치 지정 중..." → "작성 중..." 라벨 전환) — 좌표를 숫자로 직접 입력받지 않는다(§36).
  DBText/MText는 항상 사용자가 명시적으로 고른다(줄바꿈 감지로 자동 전환하지 않음, §38).
- 상단 앱 전역 Property Inspector가 선택된 행의 전체 상세(형식/내용/높이/Layer/색상/TextStyle/
  Handle) 또는 배치 요약을 보여준다 — 화면 안에 별도 Inspector 패널을 새로 만들지 않고 기존
  전역 패턴을 재사용했다.

## 11. Persistence — 이번 Milestone에는 연결하지 않는다

Excel/PDF/Plot Export와 달리, 문자 생성/수정은 `ExportRecord`/`ActivityRecord` 어느 쪽에도 기록을
남기지 않는다(§57-58, 명시적 결정). DWG 자체가 유일한 진실의 원천이고, 값 하나하나에 대해 별도
이력을 SQLite에 미러링할 필요가 이번 범위에서는 없다고 판단했다.

## 12. 실제로 검증한 것 (Simulation Mode, 2026-08-09)

FakeAutoCad.exe + Desktop.exe 두 프로세스 구성(Milestone 2-11과 동일)으로: 실제 `SelectTextObjects`
왕복(혼합 타입 2개 객체가 정확히 표시됨) → 배치 Height 변경 체크+적용(높이 250→300, 테이블 즉시
갱신, 체크박스 리셋) → 잠긴 Layer 배치 실패(전체 실패, 테이블 값 원본 유지 확인, 구체적인 한국어
오류 메시지 확인) → 연결 끊김(FakeAutoCad 종료 → 버튼 비활성화 + "AutoCAD Not Running", 기존
데이터는 그대로 유지) → 작성 모드 전환 → 내용 입력 → 실제 Acquire+Create 왕복("문자 작성 완료",
내용 필드 초기화) 전부 화면 스크린샷으로 확인. 설치된 Setup EXE(개발 빌드가 아닌 실제 배포본)로도
Simulation Mode 재현해 Text 화면 도달을 확인했다.

이 과정에서 실제 렌더링 버그 2건을 발견/수정했다(둘 다 컴파일은 통과했던 것들 — CLAUDE.md
"컴파일 성공 ≠ 동작 확인"):

1. **Visibility + DataContext 충돌**: 작성 모드 패널의 같은 엘리먼트에 `Visibility="{Binding
   IsCreateMode}"`와 `DataContext="{Binding Create}"`를 동시에 걸었더니, `DataContext`를 바꾸는
   순간 그 엘리먼트 자신의 `Visibility` 바인딩도 새 DataContext(`TextCreateViewModel`, `IsCreateMode`
   없음) 기준으로 재해석되어 조용히 깨졌다(항상 `Visible`로 폴백) — 편집/작성 두 패널이 겹쳐
   렌더링됐다. 바깥 `Grid`(Visibility, 부모 DataContext)와 안쪽 `StackPanel`(DataContext override)
   로 분리해 해결.
2. **커스텀 ComboBox 템플릿이 `DisplayMemberPath`를 닫힌 상태 표시에 반영하지 않음**: 색상
   ComboBox가 닫혀 있을 때 `CadColorDto`의 기본 `ToString()`(클래스 전체 이름)을 그대로 보여줬다 —
   `DesignTokens.xaml`의 커스텀 `ControlTemplate`이 `SelectionBoxItemTemplate`을 쓰는데, 이건
   `DisplayMemberPath`만으로는 자동 합성되지 않는 WPF 특이 동작이다. 명시적
   `ComboBox.ItemTemplate`(단순 `TextBlock` 바인딩)으로 교체해 해결 — 앱 전역 스타일이 아닌 이
   화면의 ComboBox 두 곳만 수정했다(다른 화면에 영향 없음).

## 13. 이번 범위에서 의도적으로 하지 않은 것

Rotation/TextStyle 편집(조회만), MText 서식 편집기, Annotative Scale 관리, Table/Attribute/
Dimension 텍스트 편집, 프로젝트 전체 찾기/바꾸기, OCR/AI 기반 기능, Persistence 이력 기록.
