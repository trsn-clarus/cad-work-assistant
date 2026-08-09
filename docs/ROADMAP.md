# Roadmap

진행 순서는 기술적 의존성에 따라 조정될 수 있다. 상세 요구사항은 [REQUIREMENTS.md](./REQUIREMENTS.md), 구조 결정은 [ARCHITECTURE.md](./ARCHITECTURE.md) 참조.

## Milestone 0 — Foundation

**상태: 완료 (2026-08-08)**

- [x] 개발 환경 조사 (AutoCAD 2024 확인 → net48 제약 확정, .NET 8 SDK 설치)
- [x] Repository 구조 생성
- [x] 문서 작성 (ARCHITECTURE / ROADMAP / REQUIREMENTS / AUTOCAD_INTEGRATION / design-system)
- [x] Solution + 5개 프로젝트 생성 (Core / Desktop / AutoCAD / Infrastructure / Documents) — AutoCAD 프로젝트는 실제 AutoCAD 2024 Managed API 참조까지 빌드 검증 완료
- [x] 테스트 프로젝트 2개 생성 (Core.Tests / Integration.Tests), `dotnet test` 통과
- [x] Logging (Serilog) 구현 — `%LOCALAPPDATA%\CADWorkAssistant\logs\`, 일 단위 롤링
- [x] Settings 저장/로드 구현 — `%APPDATA%\CADWorkAssistant\settings.json`, 원자적 쓰기
- [x] 기본 WPF UI Shell (Navigation, 커맨드 팔레트, 산출 테이블, Inspector, Activity Log, 상태 바) — 더미 데이터로 채워짐, 실제 AutoCAD 연동은 Milestone 1
- [x] AutoCAD Plugin `IExtensionApplication` 스켈레톤 (로드/언로드 로깅) — 실제 CWA 명령/Named Pipe는 Milestone 1
- [x] 빌드/테스트/실행 검증 (스크린샷으로 UI 렌더링 확인)
- [x] CI solution filter (`CADWorkAssistant.CI.slnf`) + GitHub Actions 워크플로
- [x] Git 초기화 + 첫 커밋

**완료 기준**: `CADWorkAssistant.exe`가 오류 없이 실행되고, 좌측 Navigation이 보이는 화면이 뜬다. → 충족 확인됨 (더미 데이터 기반 풀 Shell).

## Milestone 1 — AutoCAD Connection

**상태: 코드/자동 테스트 완료, 실제 AutoCAD GUI 스모크 테스트는 보류 (2026-08-08)**

- [x] AutoCAD Plugin: IExtensionApplication 스켈레톤 (Milestone 0에서 조기 완료)
- [x] IPC 프로토콜 설계/구현 (버전/RequestId/Framing/Handler 라우팅) — `docs/AUTOCAD_INTEGRATION.md` §5
- [x] Named Pipe 서버(`AutoCadPipeServer`, Plugin) / 클라이언트(`AutoCadPipeClient`, Infrastructure) 구현, 현재 사용자로 Pipe 접근 제한
- [x] AutoCAD API 스레드 경계 (`AutoCadDispatcher` + `ExecuteInApplicationContext`, 실제 API 존재 확인)
- [x] 실행 중인 AutoCAD 프로세스 감지 (`AutoCadDiscoveryService`)
- [x] 현재 Document(DWG 경로/저장 여부), 현재 Layout, Drawing Unit(INSUNITS→Unitless 포함) 조회 (`GetDrawingContext`)
- [x] AutoCAD 미실행/Plugin 미로드/연결 끊김을 구분하는 8종 연결 상태 (`CadConnectionState`) + 상태 전이 단위 테스트
- [x] Desktop UI: 기존 Mock 연결 표시를 실제 `AutoCadConnectionManager` 값으로 교체 (AutoCAD 미실행 시나리오 UI Automation으로 검증 완료)
- [x] 자동 테스트: Core.Tests 28개 + Integration.Tests(Fake Pipe Server, 실제 Named Pipe) 6개, 전부 통과
- [ ] **실제 AutoCAD 2024 GUI로 NETLOAD → 연결 → DWG 정보 표시 스모크 테스트** — 이 개발 PC에서는 AutoCAD GUI 구동 시 그래픽 드라이버가 불안정해져(Windows 이벤트 로그에 LiveKernelEvent 기록) 완료하지 못함. AutoCAD가 정상 동작하는 머신에서 진행 예정 (`docs/AUTOCAD_INTEGRATION.md` §8에 시나리오 11개 정리됨)
- [ ] CWA 명령 등록 — Milestone 2(Length)에서 실제 명령이 필요해지는 시점에 추가 (지금은 조회 전용 IPC라 사용자 명령이 없음)
- [ ] 다중 AutoCAD Instance 선택 UI — 서비스 계층(`SelectInstanceAsync`, `AvailableInstances`)은 준비됐지만 UI 셀렉터는 아직 없음

**완료 기준**: Desktop App에서 AutoCAD 연결 상태와 현재 열린 DWG 이름을 실시간으로 확인할 수 있다. → 코드/아키텍처/자동 테스트 기준으로는 충족. 실제 AutoCAD 화면으로 최종 확인하는 것만 남음.

## Milestone 2 — Length (첫 실사용 가능 버전)

**상태: 코드/자동 테스트/Simulation Mode 종단간 검증 완료, 실제 AutoCAD GUI 검증만 남음 (2026-08-08)**

- [x] `[CAD에서 객체 선택]` 버튼 → AutoCAD Selection 모드 진입 (`SelectLengthObjectsHandler`, `Editor.GetSelection`)
- [x] Line/Polyline/Arc 길이 계산 (`Core.Length`, AutoCAD 독립적으로 유닛 테스트 46개)
- [x] mm → m 자동 변환 (도면 단위 자동 인식, `LengthUnitConverter`) — cm/dm/km/inch/feet/yard/mile도 함께 지원
- [x] Desktop에 결과 표시 + 클립보드 복사 + "산출내역 추가"로 Quantity Sheet에 저장
- [x] 지원하지 않는 객체(Hatch 등) 혼재 선택 시 제외 개수/타입 표시, 프로그램 죽지 않음
- [x] 선택 취소(Esc)를 오류가 아닌 정상 상태로 처리
- [x] Unitless 도면에서 자동 변환하지 않고 명확히 안내
- [x] **Headless AutoCAD Simulation 인프라 구축** (`CADWorkAssistant.FakeAutoCad`, 12개 Scenario) — AutoCAD 없이 전체 Workflow를 실제 Named Pipe로 종단간 검증 가능
- [x] Integration.Tests 17개 — 실제 프로세스 2개 사이 통신, 실패 시나리오(Cancel/Timeout/Disconnect/Error), 1,000개 객체 성능 테스트
- [x] Desktop을 Simulation Mode(`CWA_USE_FAKE_AUTOCAD=1`)로 실제 실행해 전체 Workflow 수동 검증 — "255.941 m" 결과와 Quantity Sheet 저장까지 확인
- [ ] **실제 AutoCAD 2024 GUI로 검증** — 이 개발 PC는 AutoCAD GUI가 불안정해(Milestone 1에서 확인) 수행하지 못함. 항목은 `docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`에 정리
- [ ] `CWA_LENGTH` AutoCAD 명령 등록 — Desktop 없이 Plugin만 Smoke Test하기 위한 부가 기능, 실제 AutoCAD 검증이 가능해지는 시점으로 미룸 (§47)

**완료 기준**: 실제 DWG에서 여러 Polyline을 선택해 정확한 총 길이(m)를 확인할 수 있다. → **Headless Simulation으로는 완전히 충족** (School_Roof.dwg 시나리오로 정확히 255.941 m 확인). 실제 DWG/AutoCAD 기준 최종 확인만 남았다. 이 지점부터 "안정적으로 매일 쓸 수 있는" 첫 실사용 버전으로 간주한다 (§37) — 단, 실제 AutoCAD 검증 전까지는 잠정.

## Milestone 3 — Area

**상태: 코드/자동 테스트/Simulation Mode 종단간 검증 완료, 실제 AutoCAD GUI 검증만 남음 (2026-08-08)**

- [x] `[CAD에서 영역 선택]` 버튼 → AutoCAD Selection 모드 진입 (`SelectAreaObjectsHandler`, Length와 동일한 `InvokeInCommandContextAsync` 경로)
- [x] 닫힌 Polyline/Polyline2d/Circle/Ellipse/Region 면적 계산 (`Core.Area`, AutoCAD 독립적으로 유닛 테스트 29개) — Polyline3d/Hatch는 리플렉션 검증 결과 불확실성이 커서 의도적으로 Unsupported 처리 (`docs/AUTOCAD_INTEGRATION.md` §5.6)
- [x] mm² → m² 자동 변환 (`AreaUnitConverter`, Length의 선형 계수를 제곱해서 재사용) — cm²/dm²/km²/in²/ft²/yd²/mi²도 함께 지원
- [x] 닫히지 않은 Polyline/Ellipse 호 선택 시 0 m²가 아니라 Open으로 분류해 명확히 제외 안내
- [x] 여러 영역 합산 (`AreaAggregationService`) + 선택/유효/제외 개수를 구분해서 표시 (PartialSuccess)
- [x] 닫혀 있지만 면적이 0/NaN/Infinity인 경우 InvalidGeometry로 분류, Valid로 합산하지 않음
- [x] Desktop에 결과 표시 + 클립보드 복사 + "산출내역 추가"로 Quantity Sheet에 저장 (Length와 같은 QuantityRecord, `Type="Area"`)
- [x] FakeAutoCad에 Area Scenario 16개 추가 (Length 13개 + Area 16개 = 29개) — Area Integration.Tests 16개 신규 추가(Length의 기존 17개는 그대로 통과, 총 Integration.Tests 33개)
- [x] Desktop을 Simulation Mode로 실제 실행해 전체 Workflow 수동 검증 — "3,102.43 m²" 결과, PartialSuccess 배너, Error/Unitless/Empty 상태, Quantity Sheet 저장까지 확인
- [x] Length/Area 공통 UI 패턴 정리 (`design-system/pages/measurement-workspace.md`) — 헤더/제외요약/테이블/총계 4단 구조를 공유
- [ ] **실제 AutoCAD 2024 GUI로 검증** — 이 개발 PC는 AutoCAD GUI가 불안정해(Milestone 1에서 확인) 수행하지 못함. 항목은 `docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`에 정리
- [ ] `CWA_AREA` AutoCAD 명령 등록 — Length의 `CWA_LENGTH`와 같은 이유로 실제 AutoCAD 검증이 가능해지는 시점으로 미룸
- [ ] Project/Drawing 단위 Unit Override (Unitless 도면의 계산 단위 수동 지정) — §3 필수 기능이 아니라 평가만 하고 보류 (`docs/ARCHITECTURE.md` §12)

**완료 기준**: 실제 DWG에서 여러 닫힌 영역을 선택해 정확한 총 면적(m²)을 확인할 수 있다. → **Headless Simulation으로는 완전히 충족** (School_Roof.dwg 시나리오로 정확히 3,102.43 m² 확인, 4개 중 1개 열림 케이스의 PartialSuccess도 확인). 실제 DWG/AutoCAD 기준 최종 확인만 남았다.

## Milestone 4 — Vertical Area + Parapet

**상태: 코드/자동 테스트/Simulation Mode 종단간 검증 완료, 실제 AutoCAD GUI 검증만 남음 (2026-08-08)**

- [x] Vertical Area 계산기(`Core.VerticalArea.VerticalAreaCalculator`, A = L × H) + 높이 mm/cm/m 단위
      정규화 + 검증(0 이하 거부) — AutoCAD 독립적으로 유닛 테스트 (실무값 회귀: 255940.660mm×0.10m
      → 25.594 m², 295141.237mm×0.10m → 29.514 m²)
- [x] Parapet 계산기(`Core.Parapet.ParapetCalculator`) — VerticalAreaCalculator를 측면/상부면 두 번
      재사용해 조합. 한 면/양면(×2) + 상부면 포함 옵션(L × Width) — 실무값 회귀: 32.118m×1.0m 양면+
      상부폭0.15m → 69.054 m²(측면 64.236 + 상부 4.818)
- [x] 기준 길이 확보 3가지 경로 — CAD에서 새로 선택 / Length 도구 최근 측정값 재사용 / 직접 입력 —
      새 AutoCAD IPC 명령 없이 전부 Milestone 2의 `SelectLengthObjects`만 재사용 (`LengthSourceSelector`
      공유 컴포넌트, Vertical Area/Parapet ViewModel이 각자 구현하지 않도록 추출)
- [x] 높이/양면/상부면 입력 변경 시 실시간 재계산 (버튼 없이 즉시 결과 갱신)
- [x] Desktop에 Vertical Area/Parapet 패널 추가 (Length/Area와 같은 Measurement Workspace 시각
      패턴 공유) + "산출내역 추가"로 Quantity Sheet에 저장 (`QuantityRecord.MeasurementSource` 필드
      신규 추가로 CAD선택/최근측정값/수동입력 구분 보존)
- [x] Core.Tests 31개(VerticalArea 13개 + Parapet 18개) 신규, Integration.Tests 6개 신규(기존
      Length Scenario "NormalSelection" 재사용, Vertical Area/Parapet 전용 FakeAutoCad Scenario는
      만들지 않음 — §106 "계산 로직은 FakeAutoCad에 넣지 않는다" 원칙)
- [x] Desktop을 Simulation Mode로 실제 실행해 전체 Workflow 수동 검증 — 이 과정에서 실제 버그 발견/
      수정(`LengthWorkflowViewModel.LastResult`가 PropertyChanged 없이 갱신되어 "최근 측정값 사용"
      라디오가 새 측정 후에도 비활성 상태로 멈춰 있던 문제)
- [ ] **실제 AutoCAD 2024 GUI로 검증** — 이 개발 PC는 AutoCAD GUI가 불안정해(Milestone 1에서 확인)
      수행하지 못함. 항목은 `docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`에 정리 (다만 이 기능은 계산
      로직 대부분이 Core에서 완전히 테스트되므로 Real AutoCAD 의존성이 낮다 - Length acquisition
      통합만 확인하면 된다)

**완료 기준**: CAD에서 선택한 기준선에 높이를 입력해 정확한 수직면적(m²)을, 파라펫 둘레에 높이/면/
상부폭을 입력해 정확한 파라펫 총 면적을 확인하고 계산 근거와 함께 저장할 수 있다. →
**Headless Simulation + Simulation Mode 수동 검증으로 완전히 충족**. 실제 DWG/AutoCAD 기준 최종
확인만 남았다.

## Milestone 4.5 — Production UI/UX System + Professional Desktop Workspace Refinement

**상태: 완료 (2026-08-08)**

Milestone 0-4에서 기능은 실사용 가능한 수준까지 갖췄지만 UI는 여전히 Milestone 0 초기 Shell의
장식/가짜 데이터를 그대로 이고 있었다. 새 기능을 추가하지 않고 기존 4개 측정 도구(Length/Area/
Vertical Area/Parapet)와 App Shell(Dashboard/Inspector/Navigation/Connection)을 실제로 매일 쓸 수
있는 "Precision Engineering Workspace"로 재정비했다.

- [x] Simulation Mode 실제 렌더링 시각 검증(스크린샷 기반)으로 두 가지 실재 버그 발견/확인:
      (1) `PrimaryButton`이 중립 `Button` 스타일의 hover 트리거를 공유해 hover 시 거의 흰색
      배경 위에 흰 글자가 겹쳐 완전히 안 보이는 문제, (2) `_selectedTool` 기본값과 `Navigation`
      컬렉션의 `IsSelected` 초기값이 서로 달라 Dashboard가 선택된 것처럼 보이면서 실제로는 Length
      패널이 렌더링되는 문제
- [x] `DesignTokens.xaml` 개편 — `PrimaryButton`(Accent 채움, hover/press에서도 흰 글자 유지)/
      `SecondaryButton`(Accent 외곽선)/`QuietButton`(테두리 없음) 3단 버튼 계층을 각각 독립된
      `ControlTemplate`으로 분리해 hover 버그를 근본적으로 제거 — 모든 버튼을 Accent Filled로
      만들지 않는다는 원칙을 코드로 강제
- [x] 연결 상태 전용 시맨틱 브러시 별칭(`BrushConnected`/`BrushConnecting`/`BrushDisconnected`/
      `BrushConnectionError`) + Divider/TextDisabled/AccentHover/AccentPressed 토큰, Spacing
      Scale(`Space1`-`Space6`), `InlineMessageBorder`/`InlineMessageText`(경고/안내 배너 공통
      스타일), `InspectorLabel`/`InspectorValue`/`InspectorNumericValue`(Property Inspector용) 추가
- [x] 숫자+단위 Typography 분리 — Length/Area/Vertical Area/Parapet 총계 표시를 값(`NumericText`,
      큰 폰트)과 단위(`NumericUnitText`, 작고 흐린 폰트)로 나눈 두 개의 `TextBlock`으로 분리
      (`TotalValueDisplay` ViewModel 프로퍼티 + `StringNotEmptyToVisibilityConverter` 신규 추가)
- [x] Connection State를 색상만이 아니라 별도 기호로 구분(`ConnectionStatusGlyph`: ●=연결됨/
      ◐=진행 중/◇=감지됨/△=Plugin 없음/✕=끊김/!=오류/○=미실행) — 사이드바 AutoCAD 카드와 하단
      상태 바 양쪽에 적용
- [x] Navigation: 실제 화면이 있는 5개 항목(Dashboard/Length/Area/Vertical Area/Parapet)만
      활성화, 나머지 10개는 `NavItem.IsImplemented=false`로 비활성 표시(완전히 숨기지 않고 자리는
      예약) + Tooltip "(곧 제공됩니다)"
- [x] Dashboard 재정의 — 가짜 Metric 카드 4개, 아무 동작도 하지 않던 "Extract Length"/"Copy"
      버튼, 아무것도 필터링하지 않던 가짜 "Filter results" 텍스트박스, 세션 시작부터 채워져 있던
      가짜 산출내역/Activity Log 샘플 데이터를 모두 제거. `QuantityRecords`/`Activity`는 이제
      세션에서 실제로 발생한 이벤트로만 채워지며, 비어 있을 때는 행동 지향적 Empty State 문구를
      보여준다
- [x] Property Inspector를 실제 도구로 구현 — 활성 QUANTITY 도구에 따라 `MainWindowViewModel.
      InspectorRows`가 해당 ViewModel(Length/Area의 Rows+TotalDisplay, Vertical Area/Parapet의
      LengthSourceSelector+입력값)을 실시간으로 반영하고, Dashboard에서는 연결 상태/활성 도면/
      산출내역 건수/최근 활동을 보여준다. 기존의 "Calculation Mode/Rounding"(아무것도 하지 않는
      가짜 드롭다운)과 "Open Drawings"(하드코딩된 가짜 도면 3개) 섹션은 삭제(`DrawingFile`/
      `MetricItem` 모델도 함께 제거)
- [x] Accessibility 점검 — Command Palette의 "Toggle property inspector" 항목에 빠져 있던
      `AutomationProperties.Name` 추가, 라벨만 있고 실제로 동작하지 않던 `Alt+I` 단축키를
      `Window.InputBindings`에 실제로 등록, 4개 측정 패널의 모든 상호작용 컨트롤(Button/TextBox/
      ComboBox/RadioButton/CheckBox)이 `AutomationProperties.Name`을 갖고 있는지 전수 확인
- [x] Simulation Mode 전체 화면 재검증 중 실제 버그 1건 추가 발견/수정 — `ParapetWorkflowViewModel.
      FaceMode` setter가 `IsSingleFace`/`IsBothFaces` 계산 프로퍼티의 `PropertyChanged`를 raise하지
      않아 "양면" 선택 시 안내 배너("양면 계산은 동일한 기준 길이를 두 면에 적용합니다")가 표시되지
      않던 문제 — `LastResult` PropertyChanged 누락(Milestone 4)과 같은 유형의 버그, `docs/
      ROADMAP.md`/`design-system`에 반복 패턴으로 기록
- [x] 스크린샷 기반 시각 검증 도구화 — `SetProcessDPIAware()`를 호출하지 않은 PowerShell 스크린샷
      스크립트가 200% DPI 환경에서 창의 좌상단 1/4만 캡처하는 문제를 발견/수정 (스크래치패드
      스크립트, 프로젝트 코드 아님 — 재발 방지를 위해 여기 기록)
- [x] 기존 146개 테스트(Core.Tests 107 + Integration.Tests 39) 전부 통과 유지, `CADWorkAssistant.
      CI.slnf`/`CADWorkAssistant.sln` 양쪽 0 경고 0 오류
- [x] `design-system/MASTER.md`, `design-system/pages/workspace.md`,
      `design-system/pages/measurement-workspace.md` 갱신

**의도적으로 하지 않은 것**: 새 CAD 기능/계산 로직 추가, Dark Mode, CommunityToolkit.Mvvm 등 새
MVVM 패키지 도입, 개별 측정 패널 DataGrid의 행 단위 Empty State(제목 없음 배지 등 — Dashboard의
Empty State만 이번 범위), 전체 Nav 항목에 대한 실제 키보드 단축키 배선(Alt+I만 이번에 실제로
연결) — 이런 항목들은 각자 필요해지는 시점(다음 Milestone 또는 별도 세션)까지 미룬다.

**완료 기준**: 4개 측정 도구와 App Shell이 Simulation Mode에서 가짜 데이터/버튼 없이 전부 실제
바인딩으로 동작하고, hover 상태에서도 모든 버튼 텍스트가 legible하며, 연결 상태가 색상 없이도
구분 가능하다. → **Simulation Mode 시각 재검증(스크린샷 + UI Automation)으로 완전히 충족**.

## Milestone 5 — Drawing Navigation + Layer Isolation + Selection + WBLOCK Extraction

**상태: 코드/자동 테스트/Simulation Mode 종단간 검증 완료, 실제 AutoCAD GUI 검증만 남음 (2026-08-08)**

복잡한 DWG 안에서 필요한 도면/객체를 빠르게 찾고, 집중해서 보고, 안전하게 분리해 별도 DWG로
추출하는 기능. 원래 로드맵에서 별도 Milestone(8 Drawing Export, 9 Layer Tools)으로 나눠뒀던
범위를 하나로 묶어 먼저 진행했다 - Selection/Isolation/Layer/Export가 전부 같은
`SelectionSession` 하나를 공유하는 한 흐름이라 따로 만들면 상태를 중복해서 관리하게 된다.
자세한 아키텍처/실기 검증 대상 구분은 `docs/DRAWING_NAVIGATION.md` 참고.

- [x] Core DTO/집계 로직(`Core.Drawing`) - `CadBoundsDto`/`BoundsAggregator`(NaN/Infinity 방어,
      union 계산)/`CadSelectedObjectDto`/`SelectionSession`/`DrawingSelectionSummary`(타입별/
      Layer별 집계)/`CadLayerDto`/`ExportFileNameService`(파일명 제안+살균) - AutoCAD 비의존,
      단위 테스트 27개(추가로 130개 Core.Tests 전체 통과 유지)
- [x] IPC 명령 9개 추가(`GetDrawingOverview`/`ZoomExtents`/`ZoomToBounds`/`SelectDrawingObjects`/
      `IsolateObjects`/`GetLayers`/`SetLayerVisibility`/`RestoreVisibility`/`ExportSelection`) -
      13개 후보를 의미 단위로 통합(예: Selection+GetSelectedObjects를 하나로, Layer Isolate/
      Restore를 개별 토글과 같은 명령으로)
- [x] AutoCAD Handler 9개 구현 - 전부 실제 설치된 AutoCAD 2024 Managed API(acdbmgd.dll/acmgd.dll)를
      리플렉션으로 실존 확인 후 사용(추측 금지 원칙). Zoom은 `_ZOOM _E` 명령 문자열 대신
      `Matrix3d.PlaneToWorld`+`Editor.SetCurrentView` 기반 View 계산으로 구현(§22).
      Isolate/Restore는 `Entity.Visible` 토글 + `DrawingIsolationState`(Plugin 내 공유 상태)로
      "복원 = 작업 전 정확한 상태"를 보장(§45-46, 아래 참고). Export는
      `Database.Wblock(ObjectIdCollection, Point3d)` + `SaveAs`로 원본 Database를 건드리지 않는다
- [x] FakeAutoCad Handler 9개 + `FakeDrawingState`(Fake 프로세스가 Layer On/Off 상태를 자체적으로
      추적해 Isolate→Restore 왕복을 실제 Named Pipe로 검증 가능하게 함) + Scenario 9개
      (DrawingNavigationNormal 등)
- [x] Headless Integration E2E 15개 - Select→Zoom→Isolate→Restore, **Layer Restore가 "전부 On"이
      아니라 원래 Off였던 Layer(A-DOOR)까지 정확히 복원하는지 검증**(§45-46 핵심 원칙), 현재
      Layer 보호, Export 성공/실패/빈 선택, Selection 실패 4종(Cancel/Timeout/Disconnect/Error),
      1,000개 객체 성능(CADWorkAssistant.Integration.Tests 총 54개로 확대)
- [x] Desktop: `DrawingWorkflowViewModel`(Navigation+Selection 통합, §80) + `LayerWorkflowViewModel`
      (조회/실제 동작하는 검색 필터/개별 토글/"선택 Layer만 보기") + `ExportWorkflowViewModel`
      (설명 입력 시 파일명 실시간 미리보기 + native `SaveFileDialog`) - 3개로 분리하되 거대한 단일
      ViewModel은 만들지 않음(§83)
- [x] `DrawingPanel.xaml` - Milestone 4.5의 Production Design System 그대로 사용(새 색상/버튼
      스타일 없음). Selection 결과 | Layer Manager 2단 분할(GridSplitter), Isolation 상태 배너,
      Property Inspector에 Drawing 전용 Row 추가
- [x] Navigation: Selection/Layers/Export를 별도 페이지로 예약해뒀던 것을 제거하고 Drawing 하나로
      통합, `isImplemented: true`로 전환
- [x] Simulation Mode 실제 렌더링으로 버그 2건 발견/수정 - (1) `OnActivated()`가 `IsBusy` 경쟁
      상태 때문에 Layer Manager를 절대 채우지 못했던 문제, (2) `DataGridCheckBoxColumn`의 기본
      TwoWay 바인딩이 읽기 전용 속성(`IsFrozen`/`IsLocked`)에서 처리되지 않은 예외를 던져 Layer
      Manager 전체가 빈 채로 남았던 문제 - 둘 다 `docs/DRAWING_NAVIGATION.md`에 상세 기록
- [x] `docs/DRAWING_NAVIGATION.md`(신규), `docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md` Milestone 5
      섹션 대폭 추가(Zoom/Selection/Isolation/Layer/WBLOCK 각각), `design-system/pages/
      drawing-workspace.md`(신규)
- [ ] **실제 AutoCAD 2024 GUI로 검증** - 이 Milestone은 이전 Milestone들과 달리 AutoCAD 의존성이
      높다(View 조작/인터랙티브 선택/Entity Visible 변경/WBLOCK 전부 실물에서만 최종 확인 가능).
      항목은 `docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md` Milestone 5 섹션에 정리(약 45개 세부 항목)

**의도적으로 하지 않은 것**: Freeze/Thaw 토글(조회만, §42), Object Hide(선택 안 한 것만 끄기 -
Isolate가 핵심 요구를 이미 충족), Zoom Window(AutoCAD 자체 기능과 중복도 높음, §25), Polygon
Selection UI(Window/Crossing으로 핵심 요구 충족, API 존재는 확인해둠), Xref 특수 처리, Drawing
Cluster 자동 탐지(§65, 이번엔 그 기반인 Zoom/Selection/Bounds만 구축).

**완료 기준**: 영역을 선택하고, 화면에 맞추고, 선택한 것만 보고, 필요하면 Layer까지 격리했다가,
전부 정확히 원래대로 복원하고, 선택한 객체를 원본 손상 없이 새 DWG로 저장할 수 있다. →
**Headless Simulation + Simulation Mode 수동 검증으로 완전히 충족**(Zoom의 시각적 정확성과 WBLOCK
결과물의 완전성은 예외 - 실제 DWG/AutoCAD 기준 최종 확인만 남았다).

## Milestone 6 — Persistence + Project Management + Quantity/Activity History

**상태: 코드/자동 테스트/Simulation Mode 종단간 검증 완료 (2026-08-09)**

Milestone 0-5까지 "CAD Work Assistant"는 세션 기반 도구였다 - 프로그램을 닫으면 산출내역/활동
이력이 전부 사라졌다. 이 Milestone은 Project를 단위로 QuantityRecord/ActivityRecord/
DrawingFile/ExportRecord를 로컬 SQLite에 저장해 재시작 후에도 유지되는 "프로젝트 기반 업무
프로그램"으로 전환한다. 자세한 스키마/트랜잭션/테스트 전략은 `docs/PERSISTENCE.md`,
호출 구조 요약은 `docs/ARCHITECTURE.md` §8.6 참고.

- [x] `CADWorkAssistant.Persistence` 신규 프로젝트(net8.0 전용, AutoCAD Plugin이 참조하는
      Infrastructure와 분리) - `Microsoft.Data.Sqlite` raw ADO.NET(EF Core 대비 이 규모에서
      오버헤드가 크다고 판단, 기존 CommunityToolkit.Mvvm/MediatR 거절과 같은 기준)
- [x] `Core.Models`에 `Project`/`ActivityRecord`/`DrawingFile`/`ExportRecord`/
      `RecentMeasurement` 신규 + 기존 `QuantityRecord`에 `ProjectId`/`Description`/`UpdatedAt`/
      `CalculationMetadataJson` 확장 (netstandard2.0 제약으로 `record` 대신 기존 스타일 그대로
      plain class + 명시적 생성자 유지) - `OperationLogEntry`는 `ActivityRecord`로 완전히 대체
- [x] `PRAGMA user_version` 기반 마이그레이션(`IMigration`/`DatabaseMigrator`) - 6개 테이블
      (Project/QuantityRecord/ActivityRecord/DrawingFile/ExportRecord/RecentMeasurement) 전체
      생성하는 `Migration001InitialSchema` 1개로 시작. 기존 마이그레이션은 절대 수정하지 않고
      새 버전을 추가하는 절차를 문서화
- [x] Repository 6쌍(Project/QuantityRecord/Activity/DrawingFile/ExportRecord/
      RecentMeasurement) - 커넥션을 인자로 받는 상태 없는(stateless) 클래스. `ProjectDataService`가
      QuantityRecord+ActivityRecord, Project+ActivityRecord처럼 교차 테이블 원자성이 필요한
      곳만 트랜잭션으로 조립
- [x] `decimal`은 TEXT(InvariantCulture), `DateTimeOffset`은 UTC ISO-8601 TEXT로 저장하는 변환
      규칙을 `SqliteValueConverters` 한 곳에 통일 (mm/m 계수를 `DrawingUnitConversion` 한 곳에
      모은 것과 같은 이유)
- [x] `CADWorkAssistant.Persistence.Tests` 신규(25개, 전부 실제 파일 기반 SQLite - `:memory:`
      아님) - 스키마/마이그레이션, Repository별 CRUD, 트랜잭션 원자성(성공+FK 위반 강제 실패
      롤백), 앱 재시작 시뮬레이션, 다중 프로젝트 데이터 격리
- [x] Desktop: `IProjectContextService`/`ProjectContextService` - Project 미보유 시("빠른
      세션") 메모리 전용, 보유 시 DB 위임. 4개 측정 ViewModel은 Project를 여전히 모른다
      (`MainWindowViewModel`이 저장 직전 `ProjectId`를 채움, 기존 `RecordAdded` 이벤트 패턴
      그대로 재사용)
- [x] Desktop: `ProjectDialog`(생성 폼 + 최근 프로젝트 목록을 한 창에) + 사이드바 상단 프로젝트
      전환 버튼 - 별도 "Projects 페이지"를 만들지 않음(§18 원칙)
- [x] Desktop: AutoCAD 연결로 도면이 바뀔 때마다 `DrawingFile` 자동 upsert(파일 복사 없이 경로만,
      `UNIQUE(ProjectId, FullPath)`로 중복 방지), WBLOCK Export 완료 시 `ExportRecord`+
      `ActivityRecord` 자동 기록
- [x] 자동 저장 - 명시적 "저장" 버튼 없이 프로젝트 생성/산출내역 추가/Export 완료 등 각 사용자
      행동이 즉시 커밋됨
- [x] Simulation Mode(FakeAutoCad + 실제 SQLite 파일)로 전체 흐름 실제 검증 - 빠른 세션에서
      프로젝트 생성 → Length 측정("255.941 m") → 산출내역 추가 → Activity Log 확인 → 프로세스
      강제 종료(재시작 흉내) → 재실행 → 최근 프로젝트 목록에서 열기 → QuantityRecord/Activity
      정확히 복원 확인. 이 과정에서 실제 버그 2건 발견/수정: (1) `DateTimeStyles.RoundtripKind`+
      `AssumeUniversal` 동시 사용 시 `ArgumentException`(상호 배타적, 단위 테스트로 발견),
      (2) WPF `Button`에 `AutomationProperties.Name`을 명시하지 않으면 자식 `TextBlock`이 대신
      잡혀 클릭 자동화가 실패(UI Automation 클릭 검증으로 발견) - 둘 다 `docs/PERSISTENCE.md`
      §9에 상세 기록
- [x] 기존 209개 테스트(Core.Tests 130 + Persistence.Tests 25 + Integration.Tests 54) 전부
      통과 유지, `CADWorkAssistant.CI.slnf`/`CADWorkAssistant.sln` 양쪽 0 경고 0 오류
- [x] `docs/PERSISTENCE.md`(신규), `docs/ARCHITECTURE.md` §8.6 신규 + 프로젝트 구성표/의사결정
      로그/§12 갱신, `CLAUDE.md` 프로젝트 구조 갱신

**의도적으로 하지 않은 것**: Project 삭제(스키마는 FK CASCADE로 대비했으나 §170 필수 Acceptance
Criteria 밖), "최근 측정값 사용"의 재시작 후 DB 기반 자동 복구(`RecentMeasurement` 저장 자체는
동작, §92가 조건부 표현이었음), DB 암호화, 클라우드 동기화, 다중 사용자/로그인, Project ZIP
아카이브, 자동 백업 스케줄링, Excel/PDF/Plot, 비용 산정, AI Assistant - 전부 마스터 프롬프트가
명시적으로 범위 밖으로 지정했다.

**완료 기준**: 프로젝트를 만들고, 산출내역을 추가하고, 프로그램을 완전히 종료했다가 다시 열어도
프로젝트/산출내역/활동 이력이 정확히 남아 있다. → **Persistence.Tests(실제 파일 SQLite) +
Simulation Mode 실제 재시작 검증으로 완전히 충족**. AutoCAD Managed API를 새로 쓰지 않는
Milestone이라 Real AutoCAD 전용 검증 대상은 없다.

## Milestone 7 — Quantity History + Verification Engine + Review Workflow

**상태: 코드/자동 테스트/Simulation Mode 종단간 검증 완료 (2026-08-09)**

Milestone 6이 산출내역을 "저장"하는 것까지 만들었다면, 이 Milestone은 저장된 산출내역이 실제로
믿을 수 있는지 확인하는 절차를 만든다. 목표는 AI가 이상한 수량을 대신 찾아주는 것이 아니라,
프로그램이 이미 알고 있는 수학적 사실(단위 변환 계수, 저장된 산식 입력값)과 CAD provenance로
확실한 오류와 단순한 이상 가능성을 구분해, 사용자가 근거를 보고 더 빠르게 판단하도록 돕는 것이다.
자세한 설계는 `docs/QUANTITY_VERIFICATION.md`, 호출 구조는 `docs/ARCHITECTURE.md` §8.7 참고.

- [x] `Core.Verification` 신규 - `VerificationSeverity`(Pass/Info/Review/Error)와
      `QuantityReviewStatus`(Unreviewed/Verified/NeedsReview)를 분리된 두 축으로 설계(자동 검산
      결과와 사용자 검토 상태는 서로 다른 개념). `QuantityVerificationService`가 Rule 9종을
      명확한 C# 메서드로 구현(범용 Rule Engine/DSL 없음) - Finite Value, Positive Quantity, Unit
      Consistency, Raw/Converted Consistency(Length/Area), Formula Recompute(Vertical Area/
      Parapet - 실제 Calculator를 재호출해 재계산), Provenance Completeness, Duplicate Source
      Handles, Prior Record Comparison, Area/Perimeter Shape Sanity(Compactness)
- [x] Heuristic Check(중복/비교/형상)는 최대 Review까지만 쓰고 Error를 선언하지 않는다 - 면적이
      작은데 둘레가 더 긴 형상도(회귀 테스트: Area 3,100m²/Perimeter 255m vs Area 2,800m²/
      Perimeter 295m) 수학적으로 가능하므로 Error/Review가 아니라 Info로만 참고 정보를 준다
- [x] `QuantityVerificationContext`가 배치 검산 시 중복/비교/형상쌍 후보를 O(n)에 한 번만 색인해
      레코드마다 O(n²) 전체 비교를 하지 않는다
- [x] Vertical Area/Parapet의 `CalculationMetadataJson`(Milestone 6에서 컬럼만 있고 아무도 채우지
      않던 필드)을 이번에 실제로 채우기 시작 - `VerticalAreaCalculationMetadata`/
      `ParapetCalculationMetadata`(Core, 구조화 입력값)를 저장해 검산 시 `VerticalAreaCalculator`/
      `ParapetCalculator`를 그대로 재호출해서 다시 계산한다(문자열 산식을 파싱하지 않는다).
      구조화 데이터가 없는 과거 기록은 Crash 없이 "검산 불가"(Info, 산식 텍스트라도 있으면) 또는
      "계산 근거 없음"(Review, 산식마저 없으면)으로 처리
- [x] Core.Tests 30개 신규(160개 전체 통과) - 회귀값(255940.660mm→255.940660m,
      3,102,430,000mm²→3,102.43m², 32.118×1×2+32.118×0.15→69.0537m²)과 shape-sanity 두 케이스가
      전부 Error가 되지 않는지 확인
- [x] Persistence: Migration002(v1→v2, 기존 QuantityRecord/Project 등은 건드리지 않음) -
      `QuantityVerificationSnapshot`/`QuantityReview` 2개 테이블, 둘 다 QuantityRecord당 최신
      상태 하나만 남기는 upsert-latest-only(RecentMeasurement와 같은 패턴, 검산 이력 자체는
      쌓지 않기로 범위를 좁혔다 - `docs/QUANTITY_VERIFICATION.md` 참고), `ProjectDataService.
      SaveVerificationBatchAsync`(배치 전체를 하나의 트랜잭션으로), `SaveReviewAsync`(검토 상태
      변경 + Activity 기록을 사용자 행동일 때만 묶음, 자동 재검산에는 Activity를 남기지 않음)
- [x] Persistence.Tests 10개 신규(35개 전체 통과) - 새 Repository 2종, 트랜잭션 원자성, 앱 재시작
      후 검산/검토 복원, 다중 프로젝트 격리, QuantityRecord 삭제 시 FK CASCADE
- [x] Desktop: `QuantityVerificationCoordinator` - 배치 검산(취소 가능, `CancellationToken`),
      레코드 하나의 검산 실패가 배치 전체를 죽이지 않음, "산출내역 추가" 직후 자동으로 빠른 검산
      1건 실행(측정 도구는 여전히 Verification을 전혀 모른다 - `MainWindowViewModel`에서만 조립)
- [x] Desktop: `QuantityHistoryViewModel` + `HistoryPanel.xaml` - Sheet(Dashboard의 Quantity
      Results, "공식 산출내역")와 역할을 분리한 새 화면(§56, 페이지 수를 늘리는 게 목표가 아니라
      역할이 명확히 다르다). 검색/유형/검산상태/검토상태 필터, Inspector(RESULT/SOURCE/
      CALCULATION/VERIFICATION/REVIEW), 배치 검산 진행률, 체크박스 기반 다중 선택(§116, WPF
      DataGrid 멀티 선택 바인딩 대신), 2건 비교(단위 호환 시에만, 저장하지 않는 읽기 전용 분석)
- [x] Dashboard Property Inspector에 "확인 필요 N건" 한 줄 요약 추가(§105-106) - 거대한 KPI
      카드로 되돌아가지 않는다
- [x] Simulation Mode(FakeAutoCad + 실제 SQLite 파일)로 전체 흐름 실제 검증 - 프로젝트 생성 →
      Length 측정("255.941 m") → 자동 검산(5개 Check 전부 Pass) → History에서 Inspector 확인 →
      검토 메모 작성 + "검토 완료" 표시 → 앱 강제 종료 후 재시작 → 검산/검토 상태 정확히 복원 →
      동일 CAD 객체로 두 번째 측정 추가 → 중복 경고("!" Review, "동일한 CAD 객체를 사용한 유사한
      수량 기록이 이미 있습니다") 자동 표시 → 두 기록 체크 후 비교("차이: +0 m (+0%)") 확인
- [x] 이 과정에서 실제 버그 4건 발견/수정 - (1) Inspector의 숫자+단위 표시에 `TextBlock` 전용
      스타일(`NumericText`)을 `Run` 요소에 적용해 시작하자마자 XamlParseException으로 앱이
      죽던 문제, (2) `InverseBooleanToVisibilityConverter`에 nullable 참조형(`Verification`)을
      직접 바인딩해 "값이 있어도 항상 true가 아니므로 항상 Visible"이 되던 문제(bool 프로퍼티
      `HasVerification`을 추가해 해결), (3) "검토 완료" 버튼이 방금 입력한 메모
      (`ReviewNoteDraft`)가 아니라 마지막 저장본(`row.ReviewNote`)을 저장해 메모가 조용히
      사라지던 문제, (4) `DataGridCheckBoxColumn`의 TwoWay 바인딩이 이 화면의 DataGrid 설정
      조합에서 커밋되지 않아 체크해도 소스가 갱신되지 않던 문제(로그에 setter 호출 자체가 찍히지
      않는 것으로 확인 - `DataGridTemplateColumn` 안에 일반 `CheckBox`를 두는 방식으로 교체해
      해결, DataGrid 셀 편집 생명주기를 타지 않아 더 안정적이다) - 전부 `docs/
      QUANTITY_VERIFICATION.md` §9에 상세 기록
- [x] 기존 209개 테스트 + 신규 40개(Core 30 + Persistence 10) = 249개 전체 통과 유지,
      `CADWorkAssistant.CI.slnf`/`CADWorkAssistant.sln` 양쪽 0 경고 0 오류

**의도적으로 하지 않은 것**: AI/LLM 기반 판단(§152-153, 근거 없는 "이상해 보입니다" 금지),
자동 도면 리비전 비교, 사용자 정의 검산 프로필/임계값 설정 화면, 개구부 공제 검증, 비용/단가
검증, 검산 결과 이력 누적 보관(최신 1건만 upsert), Project 삭제(Milestone 6과 동일하게 범위 밖),
검산 규칙에 threshold가 필요한 곳(Compactness)도 절대적 Error 판정에는 쓰지 않음.

**완료 기준**: 저장된 산출내역이 결정적으로 잘못된 경우(단위 불일치, 원본값-저장값 불일치, 산식
재계산 불일치)를 Error로, 확인이 필요한 이상 가능성(중복 의심, 큰 폭 변화, 긴 둘레)을 Review로
구분해서 보여주고, 사용자가 검토 상태와 메모를 남길 수 있으며, 그 모든 상태가 재시작 후에도
남아 있다. → **Core.Tests + Persistence.Tests + Simulation Mode 실제 재시작 검증으로 완전히
충족**. 이 Milestone은 새 AutoCAD API를 쓰지 않아 Real AutoCAD 전용 검증 대상이 없다.

## Milestone 8 — Production Packaging + Premium UI/UX Finalization

**상태: 완료 (2026-08-09)**

두 축을 동시에 완료했다: (A) 실제 설치형 Windows 제품으로 만드는 Production Packaging, (B) 기존
7개 Milestone에서 이미 상당히 성숙해 있던 UI를 상용 제품 수준으로 마무리하는 Premium UI/UX Pass.
새 CAD 계산 기능은 추가하지 않았다(§7) - 이 Milestone의 목표는 UI 완성도와 배포 안정성이다.
상세 근거/발견 사항은 `docs/DEPLOYMENT.md`, `docs/RELEASE_CHECKLIST.md`,
`design-system/PRODUCTION_UI_REVIEW.md` 참고.

- [x] Simulation Mode로 Dashboard/Vertical Area/History/Settings 등 주요 화면 실제 스크린샷 Audit -
      P1급 문제 확인: 앱 아이콘 없음, ComboBox/CheckBox가 기본 WPF Chrome(다른 컨트롤과 시각적
      불일치), 미구현 Nav 항목 5개가 비활성 상태로 계속 노출됨, History 그리드 컬럼폭이 좁은
      pane에서 겹침
- [x] `ui-ux-pro-max` 스킬로 5개 Query 실행(design-system/wpf stack/color/style/ux) - 기존 방향
      (navy-slate B2B 팔레트, 밀도, empty-state, checkbox+action bar)이 이미 대부분 부합함을
      확인, 웹 SaaS/HUD-neon/DeFi-glassmorphism류 추천은 기각(§13)
- [x] 21st.dev CLI 확인 - 이전 Milestone 기록과 동일하게 로그인/토큰 필요, 이 세션에도 시크릿 없어
      실제 컴포넌트 검색은 수행하지 않음(기존 `.21st/DESIGN.md`에 이미 반영된 패턴 재사용)
- [x] TRSN CLARUS 브랜드 마크(정밀 선택 reticle) 디자인 - GDI+로 512px 마스터 + 7-size .ico 생성,
      Window/Taskbar 아이콘·Settings 화면·설치 프로그램 아이콘에 재사용, 사이드바 하단에 muted
      크레딧 한 줄 추가
- [x] Design Tokens: ComboBox/CheckBox/RadioButton을 Button/TextBox와 같은 flat/bordered 언어로
      재템플릿, Command Palette scrim의 유일한 raw hex(`#66000000`)를 `BrushScrim` 토큰으로 정리
- [x] Navigation: 미구현 항목(Files/Plot/PDF/Excel) 제거, 대신 실제 화면(Settings/About: 버전 +
      데이터 위치 + "데이터 폴더 열기" + TRSN CLARUS 크레딧)을 추가 - "자리만 예약해두고
      비활성화"(Milestone 4.5 §23) 방침을 이번엔 뒤집었다: 상용 제품 첫인상에서는 빈 자리보다
      클릭 안 되는 항목 5개가 더 나쁘다는 판단
- [x] History 그리드 컬럼폭 재조정 - DESCRIPTION을 `*`로, 나머지 고정폭 합계를 좁혀 좁은
      pane에서도 겹치지 않게 함
- [x] Version Source of Truth: `Directory.Build.props`에 `CwaVersion`/`Product`/`Company` 추가 -
      Desktop/Plugin/Installer/Bundle Manifest 전부 이 값 하나를 읽는다
- [x] Desktop: self-contained win-x64 publish 설정(csproj 기본값이 아니라 publish 시점에만 RID
      지정 - `dotnet build`/`dotnet run` 일상 개발 루프가 매번 win-x64 런타임 팩을 복원하지 않게)
- [x] AutoCAD Plugin Bundle(`installer/CADWorkAssistant.bundle/PackageContents.xml`) - 이 PC에
      실제 설치된 AutoCAD 2024의 내부 릴리스 ID(`R24.3`)를 `C:\ProgramData\Autodesk\AutoCAD 2024\`
      폴더명에서 직접 확인해 사용(추측 아님). `LoadOnAutoCADStartup="True"`로 NETLOAD 불필요.
      Autodesk 호스트 DLL(acdbmgd 등)은 애초에 `Private=false`라 Bundle에 들어가지 않음(기존
      설정을 실측으로 재확인)
- [x] Desktop 단일 인스턴스 보호(`Global\CADWorkAssistant.SingleInstance` Mutex) 추가 - 두
      인스턴스가 같은 SQLite 파일에 동시에 쓰는 걸 막고, Installer의 `AppMutex`가 실행 중인
      인스턴스를 감지해 종료를 안내할 수 있게 함
- [x] Inno Setup 6.7.3 설치(winget) + `installer/CADWorkAssistant.iss` 작성 - 관리자 권한 불필요
      (`PrivilegesRequired=lowest`, `{localappdata}\Programs\...`), AutoCAD 실행 중이면 강제
      종료 없이 확인 메시지만(§147), Uninstall 시 사용자 데이터(`%LOCALAPPDATA%\CADWorkAssistant\`)
      는 절대 삭제하지 않음
- [x] `scripts/build-release.ps1`(Clean→Restore→Build→Test→Publish→Plugin→Bundle→Runtime Audit→
      Installer→Hash 한 커맨드), `scripts/audit-runtime.ps1`(FakeAutoCad/테스트/소스/Node/Python/
      Autodesk 호스트 DLL이 패키지에 없는지 확인), `scripts/test-release.ps1`(설치→실행→uninstall→
      데이터 보존까지 실제로 검증)
- [x] 실제 Installer 빌드 3건의 real 버그 발견/수정 - (1) PowerShell 5.1이 BOM 없는 .ps1의 한글
      주석을 잘못된 codepage로 읽어 파서가 깨짐(스크립트를 영문으로 정리해 근본 회피), (2) Inno
      Setup에서 존재하지 않는 `createallsubdirfolders` flag를 사용해 컴파일 실패(실제 컴파일
      에러 메시지로 확인 후 제거 - `recursesubdirs` 하나로 충분), (3) `{#BundleDir}`를 Source
      경로와 Destination 폴더명 양쪽에 재사용해 AutoCAD ApplicationPlugins 아래에 전체 절대
      경로가 그대로 폴더명으로 들어가려다 설치가 Access-Denied로 롤백된 문제(`BundleFolderName`을
      따로 분리해 해결 - 설치 로그를 실제로 열어봐야 알 수 있었다)
- [x] `CADWorkAssistant-Setup-0.8.0-x64.exe`(48.9MB, SHA256 해시 포함) 실제 생성
- [x] 실제 설치 → Simulation Mode로 실행/DB 생성 확인 → 설치본 UI 재검수(Dashboard/Settings 등
      스크린샷) → Uninstall → 바이너리 제거 확인 + 사용자 DB 보존 확인, 전부 9개 체크 통과
- [x] 249개 테스트 전체 유지 통과, `CADWorkAssistant.CI.slnf`/`.sln` 양쪽 0 경고 0 오류

**의도적으로 하지 않은 것**: 새 CAD 계산 기능, Telemetry/Analytics/Crash Upload/자동 업데이트
서버(§5), Code Signing(인증서 없음 - unsigned installer로 진행, SmartScreen 경고 가능성 문서화),
File Association/Context Menu/Windows Service/자동 시작 등록, 모든 화면·해상도·DPI 조합의
전수 재검수(이미 Milestone 4.5/5/6/7에서 검증된 범위는 재검증하지 않고 이번 Milestone에서 새로
바뀐 화면만 확인), "Projects" 전용 목록 페이지 신설(§29 예시에는 있었지만 기존 `ProjectDialog`가
이미 생성+최근 목록+전환을 compact하게 처리하고 있어 중복 화면을 만들지 않기로 판단 - Remaining
Issues 참고).

**완료 기준**: `scripts/build-release.ps1` 한 커맨드로 테스트 통과 → publish → installer exe
생성까지 재현되고, 그 installer를 실제로 설치해 Simulation Mode로 실행하고 SQLite 파일이 생기는
것까지 확인했으며, 제거해도 그 파일이 남는 것까지 확인했다. → **실제 설치/실행/제거로 완전히
충족**. AutoCAD Bundle은 파일 배치까지 실제로 검증했지만 AutoCAD 실기 NETLOAD 자동 로드는 이 PC의
GUI 불안정성 제약으로 검증하지 못했다(`docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md` 다음 항목).

## Milestone 8.5 — Real AutoCAD 2024 Validation + Release Candidate Stabilization

**상태: 준비 완료, 실제 검증 BLOCKED (머신 가용성) (2026-08-09)**

새 기능 추가 없이 Milestone 8의 설치본을 실제 AutoCAD 2024 GUI로 검증해 첫 Release Candidate를
만드는 것이 목표였다. 시작 전 이 세션이 접근 가능한 머신을 사용자에게 확인했다 - 이 개발 PC
외에 다른 머신이 없었고, 이 PC는 `docs/AUTOCAD_INTEGRATION.md` §8에 기록된 대로 AutoCAD 2024
GUI 구동 시 그래픽 드라이버가 불안정해지는 이력이 있어, 사용자는 이 PC에서 위험을 다시 감수하지
않고 **준비 작업만** 진행하기로 결정했다(§7/§143의 "머신이 없으면 PASSED로 보고하지 않는다"
원칙을 그대로 따름).

- [x] Repository 상태 재확인(git status/log clean, 9개 프로젝트 확인)
- [x] `docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`를 PASS/FAIL/BLOCKED/N-A 명시 상태 표기로 개편,
      Milestone 6(Persistence 실기 Handle/Source Drawing)/7(Verification 실기 값)/8(Plugin
      Autoload, Connection 4-Case, Focus/UX, 한글 경로, 성능, Security) 섹션 신규 추가
- [x] `docs/AUTOCAD_INTEGRATION.md` §3(Bundle Autoloader "채택 예정" → "구현 완료, 실기 미검증"으로
      정정), §8(Milestone 8.5 결정 경위 기록)
- [x] `docs/REAL_AUTOCAD_VALIDATION_2024.md` 신규 - 실제 검증 세션이 채울 보고서 템플릿
      (Environment/Installation/Autoload/Connection/Length/Area/Isolation/Layer/WBLOCK/
      Persistence/Verification/Focus/Bugs/Performance/RC Decision)
- [x] `samples/validation/VALIDATION_DWG_SPEC.md` 신규 - `CWA_Validation_Basic.dwg`(+ Meters/
      Unitless 단위 변형) 사양서. 실제 DWG 바이너리는 AutoCAD 없이 생성할 수 없어 사양만 준비 -
      Arc 포함 Polyline, 자기교차 Polyline, Open/Closed 혼재, 한글 Layer/Text, Locked/Off/Current
      Layer 조합 등 체크리스트가 요구하는 모든 형태를 명세
- [x] 기존 249개 테스트 + 빌드 0 경고/0 오류 재확인(이번 세션은 소스 코드를 변경하지 않았으므로
      회귀 없음을 재확인하는 성격)

**의도적으로 하지 않은 것**: 실제 AutoCAD GUI 구동/NETLOAD/Autoload 실기 검증, 새 기능/Motion/UI
재설계(§141-142, 이 Milestone의 범위 밖), RC 버전(`0.8.1-rc.1` 등) 생성 - 실제 검증 없이는 RC를
선언하지 않는다(§7/§99/§143).

**완료 기준**: 이 Milestone 자체는 "실제 검증"이 아니라 "실제 검증을 위한 준비"만 완료 기준으로
삼는다. → **체크리스트/보고서 템플릿/DWG 사양서 준비로 완전히 충족**. 실제 AutoCAD 2024가
안정적으로 실행되는 머신이 확보되면 `docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`와
`docs/REAL_AUTOCAD_VALIDATION_2024.md`를 채우는 것으로 이 Milestone을 재개한다 - 그때까지 RC는
선언되지 않는다.

## Milestone 9 — Excel Quantity Export + Verification-Aware Deliverable Generation

**상태: 코드/자동 테스트/Simulation Mode 종단간 검증/설치본 Excel smoke test 완료 (2026-08-09)**

저장된 QuantityRecord(+최신 검산 결과+검토 상태)를 실무 제출/검토/적산 워크플로에 바로 쓸 수 있는
Excel "수량산출서"로 내보낸다. 자세한 시트 구성/정밀도 정책/보안은 `docs/EXCEL_EXPORT.md`, 호출
구조는 `docs/ARCHITECTURE.md` §8.8 참고.

- [x] 라이브러리 선정: ClosedXML 0.105.1(MIT) - Microsoft.Office.Interop.Excel(COM, Excel 설치
      필요)과 EPPlus(최신 버전 상업 라이선스 필요)를 제외하고, Excel 미설치 환경에서도 항상
      성공하는 순수 OpenXML 라이브러리를 채택
- [x] `CADWorkAssistant.Documents`를 netstandard2.0 → net8.0으로 재타겟(`Persistence`와 같은 이유
      - AutoCAD Plugin이 참조하지 않으므로 멀티타겟이 필요 없다) - `Core`/`CADWorkAssistant.AutoCAD`는
      여전히 ClosedXML을 전혀 참조하지 않는다
- [x] Core: 표시명 정책을 `QuantityTypeDisplay`/`QuantityReviewStatusDisplay`/
      `VerificationSeverityDisplay`로 추출해 화면(`QuantityHistoryRow`)과 Excel이 완전히 같은
      글리프/문구를 공유하도록 통일(회귀 없이 리팩터링, 기존+신규 테스트로 확인)
- [x] Documents: `Excel.QuantityWorkbookModel`/`QuantityWorkbookRow`(ClosedXML 비의존 순수 데이터
      모델, 향후 PDF Export가 재사용 가능) + `QuantityWorkbookModelBuilder`(Project+QuantityRecord+
      Verification/Review → Model, Verified-only 필터는 항상 `QuantityReviewStatus` 기준이고 자동
      `VerificationSeverity`로는 걸러내지 않는다 - Verified인데 Error인 레코드도 그대로 노출) +
      `QuantityWorkbookBuilder`(ClosedXML을 다루는 유일한 클래스, 4개 시트: 수량산출서/산출근거/
      검산내역/프로젝트정보, A4 가로/1페이지 폭 맞춤/헤더 반복/쪽번호, 원자적 저장)
- [x] 보안: 사용자 입력 문자열(프로젝트명/설명/검토메모)을 전부 `.Value=`/`.SetValue()`로만 쓰고
      `FormulaA1`을 쓰지 않아 `=`/`+`/`-`/`@`로 시작하는 입력이 Excel 수식으로 재해석되지 않는다 -
      4개의 실제 위험 문자열로 재오픈 검증까지 완료(추정이 아니라 실증)
- [x] Persistence: `ExportRecord.ExportType`(`DwgSelection`/`ExcelQuantity`) 신규 -
      `Migration003AddExportType`(user_version 3), 기존 WBLOCK 호출부는 기본 인자로 무변경
- [x] Desktop: `QuantityExcelExportCoordinator`(Persistence에서 새로 읽음 - 캐시된 화면 상태를
      신뢰하지 않는다, `IQuantityVerificationCoordinator` 재사용) + `ExcelExportViewModel`(scope
      라디오/4개 포함 체크박스/실시간 요약/SaveFileDialog/Success·Error 상태) + OUTPUT 그룹에
      "Excel" 화면(Ctrl+E) 신규 + `IProjectContextService.AddExcelExportRecordAsync`(내보내기
      직후 Dashboard Activity Log에 즉시 반영 - 재시작/프로젝트 전환 없이도 보인다)
- [x] Documents.Tests 신규 프로젝트(30개 테스트: Model Builder 12개 + Workbook Builder 18개) -
      회귀값(255.940660m/3,102.43m²/25.594066m²/29.5141237m²/69.0537m²) numeric cell 검증,
      Verified+Error 공존 노출, 한글 왕복, 10,000건 대량 내보내기, 수식 주입 방지, 원자적 저장 시
      기존 파일 교체, 인쇄 설정
- [x] Persistence.Tests: 실제 SQLite로 Project→QuantityRecord→Verification→Review→Excel 전체
      흐름 E2E 2건 신규(All/Verified-only 두 scope 모두 검증) + `ExportType` round-trip 1건
- [x] 회귀 전체 재확인: Core.Tests 182개(22개 신규) + Persistence.Tests 38개(3개 신규) +
      Integration.Tests 54개 + Documents.Tests 30개(신규), 총 304개 전부 통과
- [x] Simulation Mode 실제 UI 조작으로 종단간 검증 - 프로젝트 생성 → Length 측정값 산출내역 추가
      → Excel 화면에서 실시간 요약 확인 → SaveFileDialog로 실제 저장 → Success 상태(파일명/파일
      열기/폴더 열기) → Dashboard Activity Log 즉시 반영 → 저장된 실제 `.xlsx`를 재오픈해 4개
      시트/수식/글리프/계산식 문자열까지 눈으로 확인
- [x] Release 빌드/설치 프로그램 재생성 - `scripts/build-release.ps1`은 코드 변경 없이 그대로
      ClosedXML과 그 전이 의존성(DocumentFormat.OpenXml/ExcelNumberFormat/SixLabors.Fonts 등)을
      `dotnet publish` self-contained 출력에 자동 포함했다(Inno Setup `.iss`도 `SourceDir\*` 전체
      글롭이라 파일 목록을 수동으로 추가할 필요가 없었다) - 설치 프로그램을 실제로 설치하고 설치된
      EXE로 Excel Export를 다시 한번 실행해 성공을 확인(smoke test)

**의도적으로 하지 않은 것**: PDF Export(Milestone 10 후보로 `QuantityWorkbookModel` 재사용 가능하게
설계), Excel 로고 이미지 삽입(텍스트 브랜딩만), 검산 이력 append(최신 1건만 upsert하는 기존
Verification 정책을 그대로 따름), 사용자 정의 시트/컬럼 구성.

**Milestone 8.5(실제 AutoCAD 2024 실기 검증)는 이 Milestone과 무관하게 계속 BLOCKED 상태다** -
Excel Export 검증은 전부 Simulation Mode(FakeAutoCad)로 이뤄졌고, 실제 AutoCAD GUI를 이 PC에서
구동하지 않았다(§8.5 참고, 그래픽 드라이버 불안정 이슈가 해소되지 않았다).

## Milestone 10 — Plot / PDF

- [ ] Plot 설정 화면 + 프리셋 (A3/A4, 컬러/흑백)
- [ ] 기존 CTB/STB 안전하게 확인 후 적용

## Milestone 11 — Text Tools

- [ ] Text/MText 생성, 높이/색상/레이어 수정

## Milestone 12 — Project Manager 고도화

프로젝트 생성/열기/전환/최근 목록은 Milestone 6에서 이미 구현됐다(`ProjectDialog`). 여기 남는
범위는 그 이후 고도화뿐이다.

- [ ] 프로젝트 검색/필터(개수가 많아지는 시점에), Project 삭제(Milestone 6에서 스키마만 대비)
- [ ] "Projects" 전용 목록 페이지(Milestone 8에서 보류 - Card Grid 대신 Name/Client/Site/Last
      Opened dense list, `IProjectContextService.RecentProjects` 재사용)
- [ ] Files/PDF/Report 연결

## Milestone 13+ — Advanced Drawing Analysis (장기)

- [ ] Drawing Cluster 자동 탐지(도면 영역 구분)
- [ ] Drawing Intelligence (평면도/실내마감표/단면도 자동 구분)
- [ ] Automatic Quantity Takeoff
- [ ] Drawing Comparison
- [ ] Cost Estimation
- [ ] AI Assistant (자연어 명령)

이 단계들은 정확성/안정성이 검증된 이후에만 착수한다 (§33).
