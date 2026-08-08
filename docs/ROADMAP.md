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

## Milestone 5 — Quantity Sheet Persistence

- [ ] SQLite 스키마 설계 (Project, QuantityItem, CalculationHistory)
- [ ] 현재 메모리상에만 있는 Quantity Sheet(Length/Area/Vertical Area/Parapet의 "산출내역 추가"는
      이미 Milestone 2-4에서 구현 완료)를 재시작 후에도 유지되도록 영속화
- [ ] 산출내역 목록/검색 화면
- [ ] 계산 근거(산식) 보존은 `QuantityRecord.CalculationExpression`으로 이미 구현됨 - 영속화 시
      그대로 저장

## Milestone 6 — Quantity History & 검산

- [ ] 계산 이력 화면 (AutoCAD Handle 저장 → 원본 객체 재탐색)
- [ ] 수량 검산 경고 (Area/Perimeter/Compactness 기반, 단순 임계값 아님)

## Milestone 7 — Excel Export

- [ ] Excel 라이브러리 선정(라이선스/유지보수 확인)
- [ ] 산출내역 → Excel/CSV 내보내기

## Milestone 8 — Drawing Export (부분 추출)

- [ ] 영역/객체 선택 → WBLOCK 기반 별도 DWG 저장
- [ ] 파일명 자동 제안

## Milestone 9 — Layer Tools

- [ ] Layer 목록/On-Off/Freeze
- [ ] 선택 객체만 보기 / 선택 Layer만 보기 / 전체 복원

## Milestone 10 — Plot / PDF

- [ ] Plot 설정 화면 + 프리셋 (A3/A4, 컬러/흑백)
- [ ] 기존 CTB/STB 안전하게 확인 후 적용

## Milestone 11 — Text Tools

- [ ] Text/MText 생성, 높이/색상/레이어 수정

## Milestone 12 — Project Manager 고도화

- [ ] 프로젝트 목록/검색, 최근 프로젝트
- [ ] Files/PDF/Report 연결

## Milestone 13+ — Advanced Drawing Analysis (장기)

- [ ] Drawing Cluster 자동 탐지(도면 영역 구분)
- [ ] Drawing Intelligence (평면도/실내마감표/단면도 자동 구분)
- [ ] Automatic Quantity Takeoff
- [ ] Drawing Comparison
- [ ] Cost Estimation
- [ ] AI Assistant (자연어 명령)

이 단계들은 정확성/안정성이 검증된 이후에만 착수한다 (§33).
