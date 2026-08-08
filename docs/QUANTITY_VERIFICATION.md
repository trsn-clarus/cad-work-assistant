# Quantity Verification (Milestone 7)

## 1. 철학

이 Milestone의 목표는 **"AI가 이상한 수량을 찾아준다"가 아니다.** 목표는 프로그램이 이미 알고
있는 수학적 사실(단위 변환 계수, 저장된 산식 입력값)과 CAD provenance(출처 정보)를 이용해,
**확실한 오류**와 **단순한 이상 가능성**을 구분하고, 최종 판단은 사용자가 근거를 보고 내리게
돕는 것이다.

```text
확실한 불일치      → Error   (결정적 - 예: 단위 불일치, 원본값-저장값 불일치)
이상 가능성        → Review  (확인 권장 - 예: 중복 의심, 큰 폭 변화, 긴 둘레)
참고 정보          → Info    (사용자 행동을 요구하지 않음)
정상               → Pass
```

특히 "면적은 더 작은데 둘레는 더 길다"처럼 도형의 형상에 따라 얼마든지 일어날 수 있는 상황을
자동 오류로 처리하지 않는다 - 복잡하거나 길쭉한 도형이면 충분히 가능하다(§39-44, §9 참고).
모든 Warning/Error는 왜 표시되었는지 사용자가 이해할 수 있어야 한다 - Black-box score를 쓰지
않는다.

## 2. Deterministic vs Heuristic

| 구분 | 의미 | 최대 Severity | 예 |
|---|---|---|---|
| Deterministic | 수학적으로 확실하게 판단 가능 | Error | 음수 값, 단위 불일치, 원본값 변환 불일치, 산식 재계산 불일치 |
| Heuristic | 통계적·형상적·업무적으로 이상해 보임 | Review | 중복 의심, 이전 기록과 큰 차이, 낮은 compactness |

Heuristic Check는 **절대 Error를 선언하지 않는다**(§81) - 실제로 Compactness/중복/비교 세
Rule 전부 Review 또는 Info까지만 쓴다.

## 3. 아키텍처

```text
Desktop.ViewModels (Length/Area/VerticalArea/Parapet WorkflowViewModel)
  │ RecordAdded 이벤트 (Verification을 전혀 모름 - Milestone 6과 같은 원칙)
  ▼
Desktop.ViewModels.MainWindowViewModel
  │ 저장 성공 직후 자동으로 빠른 검산 1건 실행(§48)
  ▼
Desktop.Services.QuantityVerificationCoordinator (IQuantityVerificationCoordinator)
  │ Core.Verification을 실행하고 결과를 Persistence에 저장하는 조립 지점
  │ (ProjectContextService가 Quantity/Activity를 조립하는 것과 같은 역할 분담)
  ▼
CADWorkAssistant.Core.Verification.QuantityVerificationService (순수 계산, AutoCAD/DB 비의존)
  │ QuantityVerificationContext(중복/비교/형상쌍 후보를 배치당 한 번만 색인, §95-96)
  ▼
CADWorkAssistant.Persistence (QuantityVerificationSnapshot/QuantityReview Repository)
  ▼
Desktop.ViewModels.QuantityHistoryViewModel + Views.HistoryPanel
  └ Rows(QuantityHistoryRow) - QuantityRecord + 최신 Verification + 최신 Review를 한 행에
```

`Core.Verification`은 Length/Area/VerticalArea/Parapet과 같은 위치(Core, AutoCAD·WPF 비의존)에
있는 "계산" 네임스페이스다 - `QuantityVerificationResult`(방금 계산한 결과, 메모리 전용)와
Persistence의 `QuantityVerificationSnapshot`(그걸 저장하기 위해 직렬화한 형태, Core.Models)의
관계는 `LengthMeasurementResult`와 `QuantityRecord`의 관계와 같다.

### 왜 범용 Rule Engine이 아닌가

DSL/Expression Language/Dynamic Rule/Plugin Rule을 만들지 않는다(§12). 지금 필요한 9개 규칙을
`QuantityVerificationService`의 이름 있는 private 메서드로 명확하게 구현한다. 규칙이 실제로
늘어나는 시점에 재평가한다 - "필요할 때 구현한다"는 이 프로젝트의 일관된 원칙이다.

## 4. Severity와 Review Status는 서로 다른 축

자동 `VerificationSeverity`(Pass/Info/Review/Error)와 사용자의 `QuantityReviewStatus`
(Unreviewed/Verified/NeedsReview)는 독립적이다(§8). 예:

```text
Automatic check: ! 면적 대비 둘레가 긴 형상
User review:     ✓ 확인 완료
Note:            ㄱ자 평면이라 정상
```

사용자가 Verified를 선택했다고 자동 검산 결과를 지우거나 숨기지 않는다 - 둘 다 나란히 보여준다
(§66). `QuantityHistoryRow.VerificationGlyph`/`ReviewLabel`이 이 두 축을 각각 독립적으로 표시한다.

## 5. Ruleset Version

`QuantityVerificationService.CurrentRuleSetVersion`(현재 1). 저장된
`QuantityVerificationSnapshot.RuleSetVersion`이 이보다 낮으면 `QuantityHistoryRow.IsStale`이
true가 되어 History 테이블에 "(재검산 필요)"가 표시된다(§50). 규칙이 바뀌면 이 상수만 올린다 -
기존 로직을 덮어쓰지 않는다.

## 6. 구현된 규칙 9종

| # | RuleId | 대상 | 분류 | 최대 Severity |
|---|---|---|---|---|
| 1 | `FiniteValue` | 전체 | Deterministic(구조적으로 항상 Pass) | Pass |
| 2 | `PositiveQuantity` | 전체 | Deterministic | Error |
| 3 | `UnitConsistency` | 전체 | Deterministic | Error |
| 4 | `RawConversionConsistency` | Length, Area | Deterministic | Error |
| 5 | `FormulaRecompute` | VerticalArea, Parapet | Deterministic | Error(Info/Review는 메타데이터 없음) |
| 6 | `ProvenanceCompleteness` | 전체 | 정보성 | Info |
| 7 | `DuplicateSourceHandles` | ObjectHandles 있는 전체 | Heuristic | Review |
| 8 | `PriorRecordComparison` | Description 있는 전체 | Heuristic | Info |
| 9 | `ShapeSanity`(Compactness) | Area+Length 쌍 | Heuristic | Info |

### Rule 1 — Finite Value

`QuantityRecord.Value`/`RawValue`는 `decimal`이라 **구조적으로 NaN/Infinity를 표현할 수
없다**(`double`과 다르다) - 그래도 §19가 명시적으로 요구하는 방어적 검사라 규칙 자체는
남겨뒀다. 실제로 이 Check는 항상 Pass다(정직하게 문서화 - 존재하지 않는 실패를 흉내내지 않는다).

### Rule 2 — Positive Quantity

`Value <= 0`이면 Error. 모든 측정 도구는 0보다 큰 값만 저장하도록 UI 단에서 이미 막고 있어
(Vertical Area/Parapet의 높이 검증 등), DB에서 이런 값을 보면 데이터 오류일 가능성이 높다.

### Rule 3 — Unit Consistency

`Length→m`, `Area/VerticalArea/Parapet→m²`. 불일치는 Error.

### Rule 4 — Raw/Converted Consistency (Length, Area)

`RawValue`(원본 단위 값) × `DrawingUnitConversion.MetersPerUnit(SourceUnit)`(Area는 제곱)로
기대값을 다시 계산해 `Value`와 비교한다. Vertical Area/Parapet은 적용하지 않는다 - 그 둘의
`RawValue`는 "면적"이 아니라 "기준 길이"라 같은 방식으로 비교할 수 없다(대신 Rule 5가 검산한다).
Unitless/변환 계수를 알 수 없는 경우는 조용히 검사를 생략한다(Info로 소음을 내지 않는다).

원본 계산부(`LengthWorkflowViewModel`/`AreaWorkflowViewModel`)와 **완전히 같은 순서로 `double`
연산 후 `decimal`로 캐스팅**한다 - `decimal` 산술을 쓰면 원본이 `double` 기반이라 마지막 몇
자리에서 어긋날 수 있다. 두 경로가 같은 IEEE754 연산을 재현하므로 실제로 필요한 허용오차는
매우 작다(그래도 절대 `a == b`로 비교하지 않는다, §23 - 상대 오차+절대 하한 조합을 쓴다).

### Rule 5 — Formula Recompute (Vertical Area, Parapet)

저장된 `CalculationMetadataJson`(§7 참고)을 파싱해 **실제 `VerticalAreaCalculator`/
`ParapetCalculator`를 다시 호출**해서 얻은 결과와 `Value`를 비교한다. `CalculationExpression`
문자열은 사람이 읽기 위한 것이라 파싱해서 검산에 쓰지 않는다(§25).

- 메타데이터 있음 + 값 일치 → Pass
- 메타데이터 있음 + 값 불일치 → **Error**
- 메타데이터 없음 + `CalculationExpression`은 있음(과거 기록) → Info("자동 재계산 불가")
- 메타데이터도 산식도 없음 → **Review**("계산 근거가 없습니다")

### Rule 6 — Provenance Completeness

`MeasurementSource`가 `null`(Length/Area - CAD 선택만 지원해 애초에 채우지 않는다) 또는
`CadSelection`/`ExistingMeasurement`인데 `ObjectHandles`가 비어 있으면 Info. `Manual` 소스는
Handle이 없는 게 정상이라 검사하지 않는다(§30).

### Rule 7 — Duplicate Source Handles

같은 Type + 같은 SourceDrawing(대소문자 무시) + 완전히 동일한 ObjectHandle 집합(순서 무관,
정렬+대문자 정규화한 서명으로 비교)을 가진 다른 레코드가 있으면 Review. **Exact Set Match만
구현한다**(§33) - 부분 겹침(예: 10개 중 8개 동일)은 false positive가 늘어날 수 있어 이번
범위에서 제외하고 향후 후보로 남겼다. 자동으로 삭제/병합하지 않는다(§77) - 사용자가 의도적으로
같은 객체를 여러 공종에 쓴 것일 수 있다.

### Rule 8 — Prior Record Comparison

같은 Project + 같은 Type + 같은(비어 있지 않은) Description을 가진 레코드 중 이 레코드보다
먼저 만들어진 가장 최근 것과 비교해 변화율을 알려준다. **절대 임계값으로 자동 Review 판정하지
않는다**(§35) - 항상 Info로 비교 정보만 제공한다("이전 기록보다 25.6% 증가했습니다").

### Rule 9 — Area/Perimeter Shape Sanity (Compactness)

같은 SourceDrawing + 동일한 ObjectHandle 집합을 가진 반대 타입(Area↔Length) 레코드가 있으면
(같은 폐합 도형을 양쪽에서 측정한 것으로 간주) compactness를 계산한다:

```text
C = 4πA / P²
```

정사각형(C≈0.785)보다 상당히 낮은 형상을 "복잡하거나 길쭉할 수 있다"는 참고 정보로 표시한다.
기준값은 `QuantityVerificationService.CompactnessNoticeThreshold`(현재 0.5) - 명확한 상수로
선언했고, 이 값을 밑돌아도 Severity는 **Info까지만** 쓴다(Review/Error 절대 없음, §41, §80).
Description만 같다는 이유로 짝짓지 않는다(§42) - 반드시 ObjectHandle 집합이 정확히 일치해야
한다.

**Description만 같다는 이유로 자동 "Revision 1/2"라고 이름 붙이지 않는다**(§75) - 사용자가
지정하지 않은 설계 순서를 프로그램이 추측하지 않는다. 비교 UI(§10 참고)도 Record A/B 또는
날짜/설명만 쓴다.

## 7. Vertical Area / Parapet 구조화 메타데이터

Milestone 6에서 `QuantityRecord.CalculationMetadataJson` 컬럼은 이미 있었지만 아무도 채우지
않았다(문서에 "향후 정밀 재검산이 필요해지면"이라고 남겨둔 상태였다). 이번에 실제로 채우기
시작했다:

```csharp
// Core.VerticalArea.VerticalAreaCalculationMetadata
{ "sourceLengthMeters": 255.941, "heightMeters": 0.1 }

// Core.Parapet.ParapetCalculationMetadata
{ "sourceLengthMeters": 32.118, "heightMeters": 1.0, "faceMode": "both",
  "topIncluded": true, "topWidthMeters": 0.15 }
```

미터로 이미 환산된 값만 저장한다 - Vertical Area/Parapet의 `RawValue`/`SourceUnit`이 "항상
미터"라는 기존 관례(`docs/QUANTITY_COMPOSITION.md`)와 같은 이유다. `FaceMode`는 배율(정수)이
아니라 열거값 자체를 저장한다 - 배율을 저장하면 `ParapetCalculator`의 배율 규칙이 바뀌었을 때
저장된 배율이 조용히 낡아버린다.

직렬화는 `IpcJson.Options`(camelCase, `JsonStringEnumConverter`)를 그대로 재사용한다 - IPC
전용이 아니라 이미 이 앱 전체가 공유하는 JSON 정책이라 새로 만들지 않았다.

## 8. Persistence

Migration002(v2) - 기존 `QuantityRecord`/`Project` 등 v1 테이블은 건드리지 않는다.

```sql
QuantityVerificationSnapshot(Id, ProjectId, QuantityRecordId, OverallSeverity, RuleSetVersion,
  CheckedAt, ChecksJson)
  UNIQUE(QuantityRecordId)  -- upsert-latest-only

QuantityReview(Id, ProjectId, QuantityRecordId, Status, Note, ReviewedAt)
  UNIQUE(QuantityRecordId)  -- upsert-latest-only
```

**검산 이력을 쌓지 않고 최신 1건만 보존하기로 범위를 좁혔다.** 마스터 프롬프트 §15-16은 "이
기록을 당시 어떤 검산 결과로 확인했지?"를 추적하고 싶어 했지만, §50("Stale 상태")이 실제로
필요로 하는 건 "마지막으로 확인했을 때"뿐이다. 매번 재검산할 때마다 이력을 append하면 대규모
프로젝트(§89, 10,000건)에서 반복 재검산 시 저장 공간이 무한정 늘어날 수 있어, 이번 범위에서는
upsert-latest-only로 제한했다 - `RecentMeasurement`/`DrawingFile`(Milestone 6)과 같은 패턴이다.
append-only 감사 이력은 향후 후보로 남긴다.

`ChecksJson`은 `IReadOnlyList<VerificationCheckResult>`를 직렬화한 것 - 개별 Check마다 별도
child table을 두지 않는다(`CalculationMetadataJson`과 같은 "지금 SQL로 쿼리할 필요 없으면
JSON" 원칙).

배치 검산은 `ProjectDataService.SaveVerificationBatchAsync`로 전체를 하나의 트랜잭션에
묶는다 - 레코드마다 개별 커밋하면 수천 건에서 느려지고, 중간 실패 시 일부만 저장된 애매한
상태가 남는다. 검토 상태 저장(`SaveReviewAsync`)은 Activity 기록을 **선택적으로** 묶는다 -
사용자가 직접 누른 "검토 완료"/"확인 필요"에는 Activity를 남기지만, 자동 재검산에는 남기지
않는다(§54, 모든 자동 Check를 Activity에 남기지 않는다).

## 9. Simulation Mode 실제 렌더링 검증 중 발견/수정한 버그 4건

Core.Tests/Persistence.Tests를 전부 통과한 뒤에도 Desktop을 실제로 띄워 UI Automation으로
클릭해봐야 드러나는 문제가 이번에도 있었다(반복되는 교훈 - "컴파일 성공 ≠ 동작 확인"):

1. **`TextBlock` 전용 스타일을 `Run`에 적용해 앱이 시작하자마자 죽음** —
   `HistoryPanel.xaml`의 Inspector RESULT 섹션에서 숫자+단위를 `<Run Style="{StaticResource
   NumericText}">`로 표시하려 했는데, `NumericText`/`NumericUnitText`는
   `TargetType="{x:Type TextBlock}"`이다. `Run`은 `TextBlock`이 아니라
   `System.Windows.Documents.TextElement` 계열이라 `System.InvalidOperationException:
   'TextBlock' TargetType이 'Run' 요소와 형식이 일치하지 않습니다`가 `XamlParseException`으로
   감싸져 던져지고, `MainWindow.InitializeComponent()` 단계에서 발생해 **앱이 창을 띄우기도
   전에 죽었다**. 로그 파일(`%LOCALAPPDATA%\CADWorkAssistant\logs\`)의 Unhandled UI exception
   기록으로 발견 - 두 개의 별도 `TextBlock`을 `StackPanel Orientation="Horizontal"`로 나란히
   두는 방식(다른 측정 패널들이 이미 쓰던 패턴)으로 교체해 해결.
2. **`InverseBooleanToVisibilityConverter`에 nullable 참조형을 직접 바인딩** —
   `Visibility="{Binding SelectedRow.Verification, Converter={StaticResource
   InverseBoolToVisibility}}"`처럼 `QuantityVerificationResult?`(bool이 아님)를 직접 넘기면,
   컨버터의 `value is true` 패턴이 boxed bool이 아닌 값에는 항상 false로 평가되어 **Verification이
   있어도 없어도 항상 "아직 검산하지 않았습니다" 문구가 보였다**. Simulation Mode 스크린샷으로
   실제 검산 결과 5건이 표시되는데도 그 문구가 같이 떠 있는 걸 보고 발견 - `QuantityHistoryRow`에
   `HasVerification`(진짜 bool) 프로퍼티를 추가해 그걸 대신 바인딩하도록 수정.
3. **"검토 완료" 버튼이 방금 입력한 메모가 아니라 마지막 저장본을 저장** —
   `SaveReview(QuantityReviewStatus.Verified)`가 `row.ReviewNote`(DB에서 마지막으로 불러온
   값)를 저장 인자로 썼는데, 사용자가 텍스트박스에 방금 입력한 값은 `ReviewNoteDraft`라는
   별도 프로퍼티에 있었다 - "메모를 적고 바로 검토 완료를 누르는" 자연스러운 흐름에서 방금 입력한
   메모가 조용히 사라졌다(상태는 "검토 완료"로 바뀌지만 메모 칸은 비어 있었다). Simulation Mode에서
   메모를 입력하고 앱을 재시작해 메모가 사라진 걸 보고 발견 - `ReviewNoteDraft`를 저장하도록 수정.
4. **`DataGridCheckBoxColumn`의 TwoWay 바인딩이 커밋되지 않음** — History 테이블의 배치 선택
   체크박스를 `<DataGridCheckBoxColumn Binding="{Binding IsChecked, Mode=TwoWay}">`로 만들었는데,
   UI Automation으로 체크(`TogglePattern.Toggle()`과 실제 마우스 좌표 클릭 양쪽 다)해도 체크박스는
   시각적으로 체크되지만 "선택 항목 검산"/"비교" 버튼이 계속 비활성 상태로 남았다. `IsChecked`
   프로퍼티 setter에 임시 로그를 심어 확인한 결과 **setter 자체가 한 번도 호출되지 않았다** -
   `DataGridCheckBoxColumn`은 DataGrid의 셀 편집(BeginEdit/CommitEdit) 생명주기를 타야 TwoWay
   바인딩이 소스에 커밋되는데, 이 화면의 DataGrid 설정 조합(`SelectionUnit=FullRow` 등)에서 그
   생명주기가 제대로 발동하지 않았다. `DataGridCheckBoxColumn`을 `DataGridTemplateColumn` 안에
   일반 `CheckBox`를 두는 방식으로 교체해 해결 - 일반 `CheckBox`는 자기 Click에서 즉시 커밋해
   DataGrid의 셀 편집 생명주기를 타지 않는다. 같은 코드베이스의 `DrawingPanel`(Milestone 5)의
   `LayerRow.IsOn` 체크박스 열은 같은 `DataGridCheckBoxColumn` 패턴인데도 다른 DataGrid
   설정(예: SelectionUnit 조합)에서 실제로 잘 동작해왔다 - 이 버그는 컬럼 타입 자체의 결함이
   아니라 이 특정 화면의 DataGrid 설정 조합에서만 재현되었다.

## 10. 이번 범위에서 의도적으로 하지 않은 것

- **AI/LLM 기반 판단** — "이 수량은 이상해 보입니다"처럼 근거 없는 판단을 프로그램이 내리지
  않는다(§152-153). 수학/단위/provenance/기록 비교/형상 참고정보만으로 신뢰도 높은 검산을
  만들 수 있다고 판단했다.
- **검산 결과 이력 누적(append-only audit trail)** — §8 참고, 최신 1건만 upsert.
- **부분 Handle 겹침 기반 중복 탐지** — Exact Set Match만 구현(§33).
- **사용자 정의 검산 규칙/임계값 설정 화면** — Compactness 임계값 등은 코드 상수로 고정.
- **Source Drawing 파일 존재 여부 확인(Rule 11)** — File I/O를 배치 검산 경로(Core.Verification)에
  넣으면 순수 계산이라는 성격이 깨지고, 수천 건 배치에서 네트워크 드라이브 확인이 UI를 막을
  위험이 있다(§46-47). Core는 여전히 I/O를 하지 않는다는 기존 원칙(다른 Core 네임스페이스와
  동일)을 지켰다 - 필요해지면 Desktop 쪽에서 선택된 레코드에 한해 비동기로 붙이는 방향으로
  검토한다.
- **Project 삭제** — Milestone 6과 동일한 이유로 범위 밖(§102-103).
- **개구부 공제 검증, 비용/단가 검증, 도면 리비전 비교** — 전부 마스터 프롬프트가 명시적으로
  범위 밖으로 지정했다(§151).
