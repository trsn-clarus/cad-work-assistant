# Requirements

## 1. 제품 목표

설계 DWG를 받아 AutoCAD에서 도면을 확인·선택·산출(길이/면적/수량)·추출·출력하고, 계산 결과를 Excel/PDF 등 업무 문서로 정리하는 과정을 하나의 Windows 설치형 프로그램에서 수행한다. CLI가 아닌 "버튼 클릭 → AutoCAD 선택 → 자동 분석 → 결과 표시 → 저장" 흐름을 지향하며, 사용자는 AutoCAD 명령어나 시스템 변수를 몰라도 된다.

## 2. 사용자

CAD 개발자가 아니라 **AutoCAD를 매일 사용하는 현장/설계 실무자**. UI는 실무 도구답게 정보 밀도가 있고 빠르며, SaaS 대시보드 스타일(과도한 카드/그라데이션/둥근 모서리)을 지양한다 (§26).

## 3. 기능 요구사항

### 3.1 AutoCAD 연결 상태
- 실행 중 AutoCAD 감지, 현재 Document/DWG 경로/Layout/Drawing Unit 표시
- 미실행/연결 끊김을 이해하기 쉬운 메시지로 안내
- 단순 연결/미연결 두 상태가 아니라 "AutoCAD 미실행 / 감지됐지만 Plugin 미로드 / 연결 중 / 연결됨 / 재연결 중 / 끊김 / 오류"를 구분해서 보여준다 (Milestone 1에서 `CadConnectionState`로 구현, `docs/ARCHITECTURE.md` §5)
- AutoCAD가 여러 개 실행 중이면 어떤 Instance에 연결할지 선택할 수 있어야 한다 (서비스 계층은 Milestone 1에서 구현, UI 셀렉터는 후속 작업)

### 3.2 길이 산출
- Line/Polyline/Arc 등 선택 → 개별 길이 + 총 길이
- mm → m 자동 변환 (사용자가 단위를 직접 입력하지 않아도 됨)

### 3.3 면적 산출
- 닫힌 Polyline 선택 → Area 계산, mm² → m² 변환
- 여러 객체 선택 시 항목별 + 총계
- 닫히지 않은 Polyline은 명확히 안내 (자동 판별)

### 3.4 수직면 면적
- 둘레(Polyline 길이) × 높이(사용자 입력) → 면적
- 계산식을 함께 표시 (예: `255.941 m × 0.10 m = 25.594 m²`)

### 3.5 파라펫 면적 계산기
- 둘레/높이 입력, 안쪽/바깥쪽/양면/상부 포함 옵션
- 양면: `둘레 × 높이 × 2`
- 상부 폭 입력 시 상부 수평면 추가 계산 (확장 가능하게 설계)

### 3.6 객체 선택 → 별도 DWG 저장
- 영역 선택 → 저장 위치 선택 → WBLOCK 계열 API로 신규 DWG 생성
- 파일명 자동 제안 (원본명 + 구분자 + 설명)

### 3.7 영역 선택
- Window/Crossing/Polygon/Layer/Block 단위 선택 등 AutoCAD의 기존 선택 방식을 최대한 유지

### 3.8 Layer 관리
- 목록 표시, On/Off/Freeze/Unfreeze
- 선택 객체만 보기, 선택 Layer만 보기, 전체 Layer 복원

### 3.9 층별 도면 보기
- MVP: Zoom Extents / Selection Zoom
- 장기: Model Space 내 Drawing Cluster 자동 탐지 및 제안

### 3.10 Text 도구
- Text/MText 생성, 높이/색상/Layer 수정

### 3.11 출력 / Plot
- Printer/Paper/Orientation/Plot Style/Scale/Center 설정 화면
- 프리셋: A3 컬러 PDF, A3 흑백 PDF, A4 컬러 PDF, A4 흑백 PDF
- 기존 CTB/STB 설정을 임의로 훼손하지 않음

### 3.12 산출내역 저장
- 계산 결과를 프로젝트 단위로 저장: Project/Category/Description/Quantity/Unit/Source DWG/Calculation/Created At/Memo

### 3.13 계산 이력
- 모든 계산의 산출 근거(원본 값, 변환값, 산식) 보존
- 가능하면 관련 AutoCAD 객체 Handle 저장 → 추후 원본 객체 재탐색

### 3.14 수량 검산
- 단순 임계값이 아닌 Area/Perimeter/Compactness/Bounding Box/형상 복잡도 기반으로 "비정상으로 보이는" 계산만 경고

### 3.15 프로젝트 관리
- 최근 프로젝트 목록, 신규 프로젝트 생성
- 프로젝트 내 DWG/PDF/산출내역/Export/Report/Notes 관리

### 3.16 문서화
- 최소: Excel, CSV export
- 향후: PDF, 수량산출서, 산출근거서, 견적 자료

### 3.17 단위 시스템
- 지원: mm/cm/m, mm²/cm²/m²
- 내부 계산은 원본 정밀도 유지, 화면 표시만 반올림
- 사용자가 소수점 자릿수를 길이/면적 별도로 설정 가능

## 4. 비기능 요구사항

| 항목 | 요구사항 |
|---|---|
| 원본 보호 | 원본 DWG를 임의로 변경/저장하지 않는다. Read-only 분석과 Drawing Modification을 명확히 분리하고, 변경 작업 전 사용자 확인을 받는다 (§22) |
| Undo | AutoCAD 문서를 변경하는 모든 작업은 하나의 Undo 그룹으로 묶어 AutoCAD에서 한 번의 Undo로 되돌릴 수 있어야 한다 (§23) |
| 오류 처리 | 사용자에게는 이해 가능한 메시지만 노출하고("닫힌 Polyline을 선택해주세요"), 원본 Exception/Stack Trace는 개발자 로그에만 기록한다 (§24) |
| 로그 | 실행/AutoCAD 연결/파일/명령/계산 결과/Export/Plot/Exception을 구조화 로그로 기록한다. 도면 내부 민감 데이터는 과도하게 기록하지 않는다 (§25) |
| 버전 독립성 | 특정 AutoCAD 버전에 강하게 종속되지 않도록 설계하고, 실제 설치 버전은 자동 탐지한다 (§29) |
| 테스트 가능성 | 계산 로직은 AutoCAD 없이 단위 테스트가 가능해야 한다 (§32) |
| 데이터 유실 방지 | 산출내역/설정은 원자적으로 저장하고, 프로그램 비정상 종료 시에도 데이터가 손상되지 않아야 한다 |
| 하드코딩 금지 | 파일 경로, AutoCAD 버전, 특정 PC 종속 설정을 하드코딩하지 않는다 (§34) |

## 5. 스코프 밖 (초기 버전)

- AI 자연어 명령 (장기 로드맵, §33)
- Drawing Intelligence 자동 분류, Automatic Quantity Takeoff, Drawing Comparison, Cost Estimation — 정확성/안정성이 검증된 이후 착수
- 웹 애플리케이션 (주 제품 아님, §2)
