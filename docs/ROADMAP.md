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

- [x] AutoCAD Plugin: IExtensionApplication 스켈레톤 (Milestone 0에서 조기 완료)
- [ ] CWA 명령 등록
- [ ] Named Pipe 서버(Plugin) / 클라이언트(Desktop) 구현
- [ ] 실행 중인 AutoCAD 프로세스 감지
- [ ] 현재 Document(DWG 경로), 현재 Layout, Drawing Unit(INSUNITS) 조회
- [ ] AutoCAD 미실행/연결 끊김 시 사용자 친화적 메시지
- [ ] Desktop UI: "AutoCAD 연결됨 / Drawing: example.dwg" 표시

**완료 기준**: Desktop App에서 AutoCAD 연결 상태와 현재 열린 DWG 이름을 실시간으로 확인할 수 있다.

## Milestone 2 — Length (첫 실사용 가능 버전)

- [ ] `[CAD에서 길이 선택]` 버튼 → AutoCAD Selection 모드 진입
- [ ] Line/Polyline/Arc 길이 계산 (Core, AutoCAD 독립적으로 유닛 테스트)
- [ ] mm → m 자동 변환 (도면 단위 자동 인식)
- [ ] Desktop에 결과 표시 + 클립보드 복사

**완료 기준**: 실제 DWG에서 여러 Polyline을 선택해 정확한 총 길이(m)를 확인할 수 있다. 이 지점부터 "안정적으로 매일 쓸 수 있는" 첫 실사용 버전으로 간주한다 (§37).

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
