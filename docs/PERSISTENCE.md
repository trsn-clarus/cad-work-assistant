# Persistence (Milestone 6)

Milestone 0-5까지 "CAD Work Assistant"는 세션 기반 도구였다 - 프로그램을 닫으면 산출내역/활동
이력이 전부 사라졌다. Milestone 6은 Project/QuantityRecord/ActivityRecord/DrawingFile/
ExportRecord/RecentMeasurement를 로컬 SQLite DB에 저장해 "닫았다 다시 열어도 작업이 남아 있는"
업무용 프로그램으로 바꾼다.

핵심 원칙(마스터 프롬프트 §0 그대로): **사용자가 CAD Work Assistant에 맡긴 업무 기록을 잃지
않는 것**이 편의보다 우선한다 - 데이터 정합성 > 편의, 마이그레이션 안전성 > 빠른 개발,
트랜잭션 일관성 > 즉각적 UI 반응, 사용자 데이터 보호 > 자동 복구 시도. 손상된 DB를 자동으로
지우고 새로 만드는 동작은 어떤 경우에도 하지 않는다.

## 1. 프로젝트 구성

`CADWorkAssistant.Persistence`는 새 프로젝트다 - 기존 `CADWorkAssistant.Infrastructure`에
넣지 않았다. 이유: Infrastructure는 `net48;net8.0` 멀티타겟이고 AutoCAD Plugin(net48)이 그걸
참조한다. AutoCAD Plugin은 절대 SQLite 의존성을 가지면 안 된다(마스터 프롬프트 §4의 요구 구조
"AutoCAD Plugin → IPC → Desktop/Core → Persistence" - Plugin은 Persistence를 모른다). 그래서
Persistence는 `net8.0`만 타겟하고 Desktop만 참조한다.

도메인 모델(`Project`/`ActivityRecord`/`DrawingFile`/`ExportRecord`/`RecentMeasurement`,
`QuantityRecord` 확장)은 `CADWorkAssistant.Core.Models`에 있다 - AutoCAD 비의존 순수 데이터
클래스라 SQLite 의존성 없이 Core에 둘 수 있고, 이미 `QuantityRecord`가 거기 있었다.

```text
CADWorkAssistant.Persistence/  (net8.0)
  CadWorkAssistantDatabase.cs        연결 생성 + 경로 결정 + PRAGMA + 마이그레이션 트리거
  SqliteValueConverters.cs           decimal/DateTimeOffset/문자열배열 ↔ TEXT 변환 규칙 단일화
  ProjectDataService.cs              교차 테이블 트랜잭션 조립 지점 (QuantityRecord+ActivityRecord 등)
  Migrations/
    IMigration.cs                    스키마 변경 한 단계의 인터페이스
    Migration001InitialSchema.cs     v1 - 6개 테이블 전체 CREATE
    DatabaseMigrator.cs              PRAGMA user_version 비교 → 미적용 마이그레이션만 트랜잭션 적용
  Repositories/
    I*Repository.cs / Sqlite*Repository.cs   Project/QuantityRecord/Activity/DrawingFile/ExportRecord/RecentMeasurement 6쌍
```

## 2. 왜 EF Core가 아니라 raw ADO.NET(`Microsoft.Data.Sqlite`)인가

테이블 6개, 대부분 단순 CRUD + "프로젝트별 최근 N개 정렬"뿐이다. EF Core의 마이그레이션
도구/Change Tracking 오버헤드가 이 규모에서는 이득보다 비용이 크다고 판단했다 - 이 프로젝트는
지금까지 CommunityToolkit.Mvvm, Redux/MediatR/EventBus를 전부 "이 규모에 과하다"는 이유로
거절해온 일관된 판단 기준을 그대로 적용한 것이다(`docs/ARCHITECTURE.md` §11 의사결정 로그).
Repository는 커넥션을 들고 있지 않는 상태 없는(stateless) 클래스로, 매 호출마다
`SqliteConnection`/`SqliteTransaction?`을 인자로 받는다 - `ProjectDataService`가 필요한 곳에서만
커넥션+트랜잭션을 열어 여러 Repository 메서드에 넘겨준다.

## 3. 스키마 / 마이그레이션

SQLite는 별도 SchemaVersion 테이블 대신 내장 `PRAGMA user_version`을 쓴다.
`DatabaseMigrator.MigrateToLatest`는 연결이 열릴 때마다(`CadWorkAssistantDatabase.OpenConnection`)
호출되어 `user_version`과 등록된 `IMigration` 목록(`Version` 오름차순)을 비교하고, 미적용
항목만 **하나의 트랜잭션 안에서** 순서대로 적용한 뒤에만 `user_version`을 올린다. 중간에 실패하면
롤백되어 DB는 이전 버전 그대로 남는다 - "지우고 새로 만들기"는 코드 어디에도 없다.

새 스키마 변경이 필요해지면: 기존 `Migration001InitialSchema.cs`는 **절대 수정하지 않는다**
(이미 적용된 사용자 DB와 어긋난다) - 대신 `Migration002...` 같은 새 클래스를 추가하고
`DatabaseMigrator.Migrations` 배열에 등록한다.

### 6개 테이블

| 테이블 | 용도 | 특이사항 |
|---|---|---|
| `Project` | Id(GUID)/Name/Client/Site/Description/CreatedAt/UpdatedAt/LastOpenedAt | `LastOpenedAt DESC` 인덱스 - "최근 프로젝트" 목록 |
| `QuantityRecord` | 기존 `Core.Models.QuantityRecord` 전체 필드 + `ProjectId`(FK CASCADE) | `Value`/`RawValue`는 TEXT(decimal 정밀도 보존), `ObjectHandlesJson`/`CalculationMetadataJson`은 JSON TEXT(child table 대신) |
| `ActivityRecord` | 사용자용 업무 히스토리 (Serilog 진단 로그와 다름) | `(ProjectId, CreatedAt DESC)` 복합 인덱스 |
| `DrawingFile` | AutoCAD가 연 도면의 경로 참조 (파일 복사 안 함) | `UNIQUE(ProjectId, FullPath)` - 같은 파일 재오픈 시 새 행 대신 갱신(Upsert) |
| `ExportRecord` | WBLOCK Export 결과 metadata (DWG 바이너리 저장 안 함) | |
| `RecentMeasurement` | Vertical Area/Parapet "최근 측정값 사용"의 DB측 기록 | `UNIQUE(ProjectId, MeasurementType)` - 타입당 마지막 값만 의미 있어 upsert-latest-only |

`decimal`은 SQLite에 대응 타입이 없어 `InvariantCulture` TEXT로 저장한다(REAL 부동소수점은
`255.941` 같은 실무값의 정밀도를 깨뜨릴 위험). `DateTimeOffset`은 UTC로 정규화해 ISO-8601 "O"
포맷 TEXT로 저장한다(사전식 정렬 가능, 왕복 정확). 전부 `SqliteValueConverters` 한 곳에
모여 있다 - Core의 `DrawingUnitConversion`이 mm/m 계수를 한 곳에 모아둔 것과 같은 이유다.

## 4. 트랜잭션 / 원자성

지금까지 확인된 유일한 교차 테이블 원자성 요구(마스터 프롬프트 §45-46)는 "산출내역 추가"가
`QuantityRecord`와 `ActivityRecord`를 동시에 남기는 것이다 - 하나만 저장되고 다른 하나가
실패하면 안 된다. `ProjectDataService.AddQuantityRecordWithActivityAsync`가 커넥션+트랜잭션을
열어 두 INSERT를 묶고, 실패 시 롤백한다. 같은 패턴이 "프로젝트 생성 + ProjectCreated 활동
기록"(`CreateProjectWithActivityAsync`), "Export 완료 + ExportCompleted 활동 기록"에도 쓰인다.
`Persistence.Tests/ProjectDataServiceTests.cs`가 성공 경로뿐 아니라 FK 위반으로 강제 실패시켜
롤백이 실제로 두 테이블 모두에 적용되는지 검증한다(존재하지 않는 `ProjectId`로 삽입 시도 →
`QuantityRecord` INSERT가 FK 제약으로 실패 → `ActivityRecord` INSERT도 롤백되어야 함).

WAL 모드(`PRAGMA journal_mode = WAL`)를 켜서 읽기와 쓰기가 서로 막지 않게 했다. Repository는
커넥션을 오래 들고 있지 않고 작업마다 `using var connection = database.OpenConnection()`로
짧게 열었다 닫는다 - SQLite 파일 연결은 비용이 낮고, WAL이라 동시 접근 시 `busy_timeout`(5초)
안에서 재시도로 처리된다.

## 5. 연결 / 경로 결정

`CadWorkAssistantDatabase`가 유일한 진입점이다. 기본 경로는
`%LOCALAPPDATA%\CADWorkAssistant\data\cadworkassistant.db` - 기존 `AppLog`의
`%LOCALAPPDATA%\CADWorkAssistant\logs\` 관례를 그대로 따른다. `CWA_DATABASE_PATH` 환경변수로
override할 수 있다(테스트/개발용, 기존 `CWA_USE_FAKE_AUTOCAD` 패턴과 동일한 방식). Simulation
Mode(`CWA_USE_FAKE_AUTOCAD=1`)에서는 파일명이 `cadworkassistant.simulation.db`로 자동으로
갈라진다 - 시뮬레이션 데이터가 실제 업무 데이터와 절대 섞이지 않게 하기 위함이다. DB 파일은
git에 커밋하지 않는다.

## 6. Desktop 통합

`IProjectContextService`/`ProjectContextService`(Desktop/Services)가 "지금 어떤 Project가
열려 있는가"와 그 Project의 `QuantityRecords`/`Activity` `ObservableCollection`을 소유한다.
`MainWindowViewModel`은 이 컬렉션을 그대로 노출만 하고 소유하지 않는다(기존 `RecordAdded`
이벤트 패턴을 그대로 재사용 - `LengthWorkflowViewModel` 등 4개 측정 도구는 여전히 Project를
전혀 모른다, `MainWindowViewModel`이 저장 직전에 `ProjectId`를 채운다).

**빠른 세션**: Project를 만들지 않고도 측정은 계속할 수 있다(마스터 프롬프트 §21) -
`CurrentProject`가 null이면 `QuantityRecords`/`Activity`는 메모리에만 쌓이고 DB에 저장되지
않는다. 사이드바에 "빠른 세션 (프로젝트 없음)"으로 표시된다.

**Project 생성/전환**: `ProjectDialog`(Window, 자체 `ProjectDialogViewModel`) 하나로
새 프로젝트 생성 폼 + 최근 프로젝트 목록을 같이 보여준다(§18의 "너무 많은 페이지로 쪼개지
않는다" 원칙을 Project UI에도 그대로 적용, 별도 "Projects 페이지"를 만들지 않았다). 사이드바
상단 버튼(현재 프로젝트 이름 표시)을 누르면 열린다.

**자동 저장**: 명시적 "저장" 버튼이 없다 - 산출내역 추가/프로젝트 생성/Export 완료 등 각
사용자 행동이 즉시 DB에 커밋된다(§158).

## 7. 이번 범위에서 의도적으로 하지 않은 것

- **Project 삭제** — 마스터 프롬프트 §53-56에서 논의는 됐지만 필수 Acceptance Criteria
  목록(§170의 "Projects" 항목)에는 없다. 스키마는 FK `ON DELETE CASCADE`로 이미 대비되어 있어
  나중에 Repository 메서드 하나만 추가하면 된다. QuantityRecord 삭제는 이번에 구현했다(요구
  범위에 명시).
- **"최근 측정값 사용"의 재시작 후 자동 복구** — `RecentMeasurement` 테이블/Repository/저장은
  전부 실제로 동작하고 테스트도 있지만, 앱 재시작 후 `LengthSourceSelector`의 "최근 측정값
  사용" 라디오를 DB 값으로 자동 채우는 것은 연결하지 않았다. 마스터 프롬프트 §92 자체가
  "구현한다면"이라는 조건부 표현이었다.
- **DB 암호화, 클라우드 동기화, 다중 사용자/로그인, Project ZIP 아카이브, 자동 백업 스케줄링** —
  마스터 프롬프트가 명시적으로 이번 범위 밖으로 지정했다.

## 8. 테스트 전략

`:memory:` SQLite가 아니라 매 테스트마다 새 임시 경로에 실제 파일을 만든다
(`TestDatabaseFixture`) - WAL 사이드카(`-wal`/`-shm`) 정리까지 포함. 이유: 이 Milestone의
핵심 리스크는 "메모리에서는 맞는데 실제 파일 I/O·재시작에서 깨지는 것"이라 파일 기반이어야
의미가 있다.

- **스키마/마이그레이션**: 첫 오픈에 6개 테이블이 다 생기는지, 두 번째 오픈이 기존 데이터를
  건드리지 않는지, `foreign_keys` PRAGMA가 실제로 켜져 있는지
- **Repository별 CRUD**: 각 6개 Repository의 Insert/Update/Delete/조회 정렬
- **트랜잭션 원자성**: `ProjectDataServiceTests` - 성공 경로 + FK 위반 강제 실패로 롤백 검증
- **앱 재시작 시뮬레이션**(`AppRestartSimulationTests`) - 첫 `CadWorkAssistantDatabase`
  인스턴스로 쓰고 완전히 버린 뒤, 같은 파일 경로로 새 인스턴스를 만들어 다시 읽는다 - "프로세스
  재시작"을 흉내낸다
- **다중 프로젝트 격리**(`MultiProjectIsolationTests`) - 두 프로젝트에 같은 종류 데이터를 넣고
  서로 섞이지 않는지, `RecentMeasurement`의 upsert 키가 프로젝트별로 분리되는지

`tests/CADWorkAssistant.Persistence.Tests` 25개, 전부 통과. 기존 Core.Tests(130개)/
Integration.Tests(54개)는 이번 변경으로 회귀 없음.

## 9. Simulation Mode 실제 렌더링 검증 중 발견/수정한 버그 2건

Level 1/2(단위+헤드리스 테스트)를 전부 통과한 뒤에도 Desktop을 실제로 띄워 UI Automation으로
클릭까지 해봐야 드러나는 문제가 이번에도 있었다(Milestone 5의 "컴파일 성공 ≠ 동작 확인" 교훈이
반복됨):

1. **`DateTimeStyles.RoundtripKind`+`AssumeUniversal` 동시 사용 시 `ArgumentException`** —
   `SqliteValueConverters.ParseDateTimeOffset`이 두 플래그를 함께 넘겼는데, 이 둘은 .NET에서
   상호 배타적이다. `ToDbText`가 이미 UTC `DateTime`을 `"O"` 포맷(Kind=Utc → 문자열 끝에 자동으로
   `Z`가 붙음)으로 쓰기 때문에 `RoundtripKind` 하나만으로 충분했다. Persistence 단위 테스트를
   실제로 돌려서(컴파일은 문제없었다) 25개 중 18개가 이 예외로 실패하며 드러났다 - 곧바로
   `RoundtripKind`만 남기고 `ToUniversalTime()`으로 명시 변환하도록 수정했다.
2. **WPF `Button`의 `AutomationProperties.Name`을 명시하지 않으면 자식 `TextBlock`이 대신
   잡힌다** — `ProjectDialog.xaml`의 "최근 프로젝트" 목록 `Button`(Content가 프로젝트명/발주처를
   담은 `StackPanel`)에 `AutomationProperties.Name`을 따로 지정하지 않았더니, UI Automation의
   이름 기반 검색이 Button 자신이 아니라 그 안의 `TextBlock`(자기 `Text`가 자동으로 접근성
   이름이 된다)을 먼저 찾아 `InvokePattern`이 없다는 예외를 냈다 - 실제 클릭 자동화 스크립트로
   "프로젝트 열기"를 시도했을 때 실제로 클릭이 씹히는 것으로 드러났다(스크립트 자체 예외 처리
   미비로 겉보기엔 "성공"처럼 보여 더 찾기 어려웠다). `Button`에 `AutomationProperties.Name=
   "{Binding Name, StringFormat='프로젝트 열기: {0}'}"`를 명시해 해결 - 새 프로젝트 폼의
   발주처/현장/설명 `TextBox`들도 라벨 `TextBlock`과 이름이 겹치는 같은 문제가 있어
   `"발주처 입력"` 식으로 구분되는 이름을 명시했다.

Simulation Mode(FakeAutoCad `NormalSelection` Scenario + 실제 파일 SQLite DB)로 실제 검증한
전체 흐름: 빠른 세션에서 새 프로젝트 생성 → Length 측정 후 "산출내역 추가"(255.941 m,
실무 예시값과 일치) → Activity Log에 `ProjectCreated`/`QuantityAdded` 두 건 정확히 표시 →
프로세스 강제 종료(앱 재시작 흉내) → 같은 DB 경로로 재실행 → Project Dialog 최근 목록에
프로젝트가 남아 있음 → 열면 QuantityRecord/Activity가 정확히 복원됨.

## 10. Real AutoCAD 의존성

이 Milestone은 AutoCAD Managed API를 전혀 새로 쓰지 않는다 - `DrawingFile` 등록에 쓰는
`FullPath`/`Units`는 Milestone 1의 기존 `GetDrawingContext` 응답을 그대로 재사용한다. 따라서
Real AutoCAD 전용 검증 대상이 없다.
