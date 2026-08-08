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

- [ ] 닫힌 Polyline 판별
- [ ] Area 계산 + mm² → m² 변환
- [ ] 닫히지 않은 Polyline 선택 시 명확한 안내 메시지
- [ ] 여러 객체 합산 (층별 소계 + 총계)

## Milestone 4 — Quantity Sheet

- [ ] SQLite 스키마 설계 (Project, QuantityItem, CalculationHistory)
- [ ] Length/Area 결과를 프로젝트에 "산출내역 추가"
- [ ] 산출내역 목록 화면
- [ ] 계산 근거(산식) 보존 (§17)

## Milestone 5 — Vertical Area / Parapet

- [ ] 둘레 × 높이 계산기 + 계산식 표시
- [ ] 파라펫 계산기 (안쪽/바깥쪽/양면, 상부 포함 옵션)

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
