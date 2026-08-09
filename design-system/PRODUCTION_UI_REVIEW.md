# Production UI Review (Milestone 8)

Milestone 8의 "Premium UI/UX Finalization" 축의 기록. `MASTER.md`는 토큰/컴포넌트 규칙의
단일 소스이고, 이 문서는 이번 Milestone에서 실제로 무엇을 감사했고 무엇을 바꿨는지, 무엇을
의도적으로 바꾸지 않았는지를 남긴다.

## 1. 출발점

Milestone 4.5/5/6/7을 거치며 이 앱의 UI는 이미 상당히 성숙해 있었다: 색상/타이포/spacing 토큰
체계, 3단 Button 템플릿, border-centric/no-shadow 디자인, 5-state 연결 상태 glyph, empty
state 문구, DataGrid 밀도 규칙까지 전부 이전 Milestone에서 확립됐다. 그래서 이번 Pass는
"제로베이스 재설계"가 아니라 "실제로 화면을 띄워보고 남은 격차를 찾아 좁히는" 감사 + 보정
작업이었다.

## 2. UI Audit (Simulation Mode 실제 스크린샷)

`CWA_USE_FAKE_AUTOCAD=1` + `FakeAutoCad.exe --scenario NormalSelection`로 Desktop을 띄우고
DPI-aware 캡처(200% 스케일 디스플레이)로 Dashboard/Vertical Area/History를 확인했다.

**발견한 문제 (심각도순)**:

| 등급 | 내용 |
| --- | --- |
| P1 | 앱 아이콘이 전혀 없음 (Window.Icon, exe 아이콘, 작업 표시줄 전부 기본값) |
| P1 | ComboBox/CheckBox/RadioButton이 기본 WPF Chrome - Button/TextBox/DataGrid의 flat/bordered 언어와 확연히 어긋남 (VerticalArea/Parapet 단위 선택 ComboBox에서 3D 베벨 확인) |
| P1 | 미구현 Nav 항목(Files/Plot/PDF/Excel/Preferences) 5개가 비활성 상태로 계속 노출 - 첫인상에서 "안 되는 버튼 5개"로 읽힘 |
| P1 | About/Settings 화면 없음 - 버전/데이터 위치를 확인할 방법이 없었음 |
| P2 | History 그리드가 좁은 목록 pane에서 UNIT 컬럼 헤더가 "UI" 두 글자로 잘림 (DESCRIPTION이 고정폭 150이라 합계가 pane보다 넓었음) |
| P2 | Command Palette 오버레이의 scrim이 raw hex(`#66000000`)로 토큰화되지 않음 |

P0(사용성을 망치는 크래시/조작 불가)는 발견되지 않았다 - 이미 여러 Milestone의 Simulation Mode
검증을 거쳐온 화면들이라 예상된 결과다.

## 3. UI UX Pro Max

로컬 `ui-ux-pro-max` 스킬(Python, 네트워크 불필요)로 5개 이상 Query 실행:

1. `--design-system "enterprise engineering desktop application CAD professional workspace dense"`
2. `--stack wpf "dense data table property inspector enterprise"`
3. `--domain color "technical professional tool navy slate desktop"`
4. `--domain style "quiet confident precision technical minimal"`
5. `--domain ux "loading feedback empty state data table accessibility"`

**채택**: 없음(신규 채택) - Query 2/3/5의 결과가 이미 이 앱에 구현되어 있는 패턴(IValueConverter
기반 Visibility 변환, DataTemplate 기반 ComboBox 아이템, navy 계열 팔레트, empty state
문구+action, checkbox+action bar 배치, active nav 강조)과 사실상 동일해 검증(confirmation)
자료로만 썼다.

**기각**:
- Query 1의 "Enterprise Gateway" 패턴(Hero/Mega menu/Contact Sales 웹 랜딩 구조) - 웹 SaaS
  마케팅 페이지 구조, 이 앱과 무관.
- Query 2의 "CommunityToolkit.Mvvm 사용" 권고 - 이미 확정된 아키텍처 결정(자체 `ObservableObject`/
  `RelayCommand`)과 배치, `design-system/MASTER.md`에도 명시된 "필요해지기 전엔 바꾸지 않는다"
  원칙 위반이라 기각.
- Query 4의 결과 3건(HUD/Sci-Fi neon, Bitcoin DeFi glassmorphism+gradient, Minimal 랜딩
  페이지) - 전부 `style` 도메인 검색 결과가 이 앱과 무관한 스타일 카테고리로 쏠린 것. neon/
  glassmorphism/gradient는 이 프로젝트의 명시적 Anti-Pattern(`MASTER.md`)이라 전량 기각.

## 4. 21st.dev

`.21st/DESIGN.md`가 이미 Milestone 0에서 초기화되어 있고(sidebar navigation, data table,
property inspector, command palette, activity log 패턴 기록), 이번 세션에서 `npx
@21st-dev/cli@latest --help`를 재확인했으나 컴포넌트 검색은 `21st login`/`TWENTYFIRST_TOKEN`이
필요하고 이 세션에 시크릿이 없어(이전 Milestone과 동일한 결론) 실제 검색은 수행하지 않았다.
기존에 이미 채택된 IA 패턴(사이드바 그룹 네비게이션, 밀도 높은 테이블, property inspector,
command palette, activity log)을 그대로 재사용했다.

## 5. Final Design Direction

**Precision Engineering Desktop** (기존 MASTER.md 방향 유지, 재확인). AutoCAD/Autodesk utility
tools/Bluebeam/전문 견적 소프트웨어 계열을 참고하되 복제하지 않는다. Dense/Professional/
Precise/Quiet/Confident/Technical/Fast/Native. 이번 Pass가 바꾼 건 이 방향 자체가 아니라
이 방향에서 비어 있던 구멍들(입력 컨트롤 일관성, 브랜드 마크, 미구현 항목 노출)이다.

## 6. 실제로 바꾼 것

### 6.1 Design Tokens

- `BrushScrim` 토큰 추가 (Command Palette 오버레이의 유일한 raw hex 제거).
- `ComboBox`/`ComboBoxItem`/`ComboBoxToggleButton`/`CheckBox`/`RadioButton` 전체 재템플릿 -
  기존 `Button`/`TextBox` 스타일과 같은 `BrushBorder`/`BrushSurface`/`RadiusSm` 언어. Toggle
  상태(체크됨)는 `BrushAccent` 배경 + 흰 체크마크/dot으로 통일.

### 6.2 App Shell

- `MainWindow.xaml`에 `Icon="/Assets/AppIcon.ico"` 추가.
- Navigation에서 미구현 항목(Files/Plot/PDF/Excel) 제거. "Preferences"(비활성)를
  "Settings"(구현됨)로 교체.
- 신규 `SettingsPanel.xaml` + `SettingsViewModel` - 앱 이름/버전/로고, 데이터 위치 + "데이터
  폴더 열기" 버튼, "Developed by TRSN CLARUS" 크레딧. `MainWindowViewModel`에 `IsSettingsToolSelected`
  플래그와 Inspector 케이스 추가.
- 사이드바 하단에 "TRSN CLARUS" muted 크레딧 한 줄 추가.

### 6.3 History

- `DataGridTextColumn`의 DESCRIPTION을 `Width="*"`로, 나머지 고정폭 컬럼들을 전체적으로
  좁혀(체크박스 30/DATE 82/TYPE 80/QTY 76/UNIT 44/VERIFICATION 126/REVIEW 64) 좁은 pane에서도
  컬럼이 겹치지 않게 함. 헤더가 여전히 축약되어 보일 수 있으나(`U.` 등) 이는 DataGrid의 정상적인
  ellipsis 동작이고, 전체 값은 항상 오른쪽 Inspector 상세 패널에서 확인 가능하다.

### 6.4 Branding (TRSN CLARUS)

`scripts`가 아니라 GDI+ 기반 1회성 생성 스크립트(세션 스크래치패드)로 512px 마스터 PNG를
그리고, 16/24/32/48/64/128/256 7개 사이즈를 각각 다시 그려(단순 리사이즈가 아니라 각 크기마다
획 두께를 재계산 - 작은 크기에서 획이 뭉개지지 않게) 수동으로 ICO 컨테이너를 조립했다. 디자인은
"정밀 선택 reticle" - 뷰파인더 코너 브래킷 4개 + 중앙 점, 이 제품의 핵심 동작(CAD 객체를
정밀하게 선택/측정)을 직접 은유한다. 범용 기어/로봇/AI 아이콘을 피했다(§94: "왜 이 아이콘이
필요한가?"에 답할 수 있는 디자인). Accent 색(`#1D6F8F` → `#124657` 세로 그라디언트, 배지
배경에 한정된 예외 - 텍스트/버튼에는 그라디언트를 쓰지 않는다) 위에 흰색 단색 마크만 사용해
16px에서도 최소한 배지 색상만은 인식된다.

적용처: Window/Taskbar 아이콘, Settings 화면 로고, Installer 아이콘(`SetupIconFile`), 사이드바
푸터 텍스트 크레딧.

## 7. 기각한 UI 아이디어

- **"Projects" 전용 목록 페이지 신설** - `docs/ROADMAP.md` Milestone 8 항목의 "의도적으로 하지
  않은 것"에 기록. 기존 `ProjectDialog`가 이미 생성+최근 목록+전환을 컴팩트하게 처리하고
  있어(Milestone 6), 새 전체 페이지를 만드는 것은 이번 Pass의 "UI 폴리시, 새 기능 아님"
  범위(§7)를 벗어난다고 판단해 Remaining Issues로 남겼다.
- **Motion(페이지 전환/Inspector 콘텐츠 트랜지션 등) 전면 도입** - §72-76이 적극 허용했지만,
  이 코드베이스가 과거 WPF 바인딩 버그(DataGridCheckBoxColumn, Run/TextBlock 스타일 불일치 등)를
  여러 번 겪은 이력을 감안해 검증되지 않은 애니메이션 트리거를 새로 넓게 까는 대신 범위를
  좁혔다 - 이번 Pass에서는 새 Motion을 추가하지 않았다(기존 `SystemParameters.
  ClientAreaAnimation` 존중 원칙은 그대로 유지). Remaining Issues 참고.

## 8. Accessibility / DPI

새로 추가한 컨트롤(ComboBox/CheckBox/RadioButton 재템플릿, SettingsPanel)도 기존 원칙을
그대로 따른다 - `AutomationProperties.Name`이 필요한 아이콘 전용 버튼에는 계속 명시했고,
색상 단독으로 상태를 표시하지 않는다(체크박스/라디오는 형태 자체가 상태를 전달, 텍스트 레이블이
항상 옆에 있다). 이번 Pass의 검증은 200% DPI 디스플레이 1개 해상도(2880x1800 물리, 1440x900
논리)에서 진행했다 - Milestone 4.5에서 이미 100/125/150/200% DPI 전수 검증을 마쳤으므로
이번엔 새로 바뀐 화면(Settings, 재템플릿된 입력 컨트롤)만 재확인했다.

## 9. Remaining Visual Debt

- 신설 "Projects" 목록 페이지 (위 §7).
- Motion 확장(Inspector 콘텐츠 전환, 행 삽입 강조 등, §72-76이 허용한 범위 중 이번엔 시도하지
  않은 부분).
- History 그리드 컬럼 헤더가 여전히 좁은 pane에서 축약 표시된다(`U.` 등) - 기능은 완전하지만
  시각적으로 더 다듬을 여지가 있다. Inspector 조절식 GridSplitter 폭 저장(§85-86)도 아직 없다.
- 1280x720/1366x768 초저해상도에서 이번 Pass의 신규 화면(Settings)까지 포함한 전수 재검수는
  하지 않았다 - Milestone 4.5의 기존 검증 결과를 신뢰하고 신규 화면만 200% DPI 1개 해상도로
  확인했다.
