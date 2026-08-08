# Quantity Composition (Vertical Area, Parapet)

Milestone 4에서 처음으로 CAD에서 측정한 값(Length)을 현장 조건(높이/면/상부 폭)과 조합해 실제 공사
수량으로 만드는 기능이 생겼다. 이 문서는 그 조합 규칙과 가정을 한 곳에 모은다 - "이 69.054㎡가 왜
나왔는지" 나중에 사용자와 개발자 모두 이 문서 하나로 답할 수 있어야 한다.

## Vertical Area 공식

```text
A = L × H
```

- `L`: 기준 길이(m) - Length 도구가 이미 정규화한 값. Core는 이 값이 CAD에서 왔는지, 수동 입력인지
  모른다 (§6, §9의 원칙 참고).
- `H`: 높이(m) - 사용자가 mm/cm/m 중 하나로 입력한 원본 값을 `DrawingUnitConversion.MetersPerUnit`로
  정규화한다.
- 중간 계산은 반올림하지 않는다 - `255.940660 × 0.10 = 25.594066`을 그대로 유지하고, 표시 단계에서만
  소수 3자리로 반올림한다(`25.594 m²`). Length(3자리)와 일관되게, Area의 기본 2자리 표시와는 별개로
  Vertical Area/Parapet은 마스터 요구사항의 실무값 예시(`25.594`, `29.514`, `69.054`)가 전부 3자리라
  이 두 기능만 `AreaFormatter`를 3자리로 호출한다.

`CADWorkAssistant.Core.VerticalArea.VerticalAreaCalculator`가 이 계산을 전담한다.

## Parapet 공식

Parapet은 별도 계산 엔진이 아니라 VerticalAreaCalculator를 두 번 재사용한 조합이다:

```text
측면 = (L × H) × FaceMultiplier      (FaceMultiplier: 한 면=1, 양면=2)
상부 = L × Width                      (상부면 포함 시에만, Width를 H 자리에 그대로 넣는 것과 같은 계산)
합계 = 측면 + 상부
```

`CADWorkAssistant.Core.Parapet.ParapetCalculator.Calculate`는 내부적으로
`VerticalAreaCalculator.Calculate`를 측면용으로 한 번, 상부면용으로 한 번(포함된 경우) 호출한다 -
"상부면 면적"도 결국 "길이 × 다른 한 변"이라는 같은 모양의 계산이기 때문이다.

### 양면 계산의 가정

```text
A_sides = L × H × 2
```

이 산식은 **내측/외측 둘레가 벽 두께 때문에 실제로는 다를 수 있다는 것을 알면서도, 같은 기준
길이를 양쪽 면에 동일하게 적용하는 간편 산식**이다. 화면에는 이 가정을 짧은 안내 문구로 표시한다
("양면 계산은 동일한 기준 길이를 두 면에 적용합니다"). 독립된 내측/외측 길이 입력은 이번
Milestone에서 구현하지 않았다 - Roadmap의 향후 후보로 남겨둔다.

### 상부면 계산의 범위

상부면은 단순 평면(`L × Width`)만 계산한다. 모서리, coping 경사, cap 두께, 복잡한 단면 형상은
다루지 않는다. 공제(창/문 개구부 등)도 이번 범위 밖이다.

## 기준 길이 확보 (Source)

Vertical Area와 Parapet 둘 다 기준 길이를 세 가지 방법으로 확보할 수 있다 - 새 AutoCAD IPC 명령
없이, 전부 Milestone 2의 `SelectLengthObjects`와 `Core.Length`를 그대로 재사용한다:

| Source | 설명 | Object Handle 보존 |
|---|---|---|
| CAD에서 새로 선택 | 이 화면의 "CAD에서 기준선/둘레 선택" 버튼 - `SelectLengthObjects`를 새로 호출 | O |
| 최근 측정값 사용 | Length 도구가 마지막으로 성공한 측정값(`LengthWorkflowViewModel.LastResult`)을 재사용 | O |
| 직접 입력 | 사용자가 숫자+단위를 입력 - AutoCAD와 전혀 연결되지 않는다 | X (빈 목록) |

세 Source 모두 `CADWorkAssistant.Desktop.ViewModels.LengthSourceSelector`(합성 컴포넌트, 상속이
아니라 각 Workflow ViewModel이 필드로 갖는다)가 처리한다 - Vertical Area와 Parapet에 똑같이 필요한
로직을 두 번 구현하지 않기 위해서다.

Length 측정이 Unitless 도면에서 나온 경우, 자동으로 계산을 진행하지 않는다("기준 길이 단위를 확인할
수 없습니다") - Milestone 3에서 보류한 Project Unit Override가 아직 없기 때문에, 여기서 임시로
단위를 추정하지 않는다.

## Provenance (QuantityRecord)

`QuantityRecord`는 Length/Area와 같은 필드를 그대로 쓰되, 새 필드 하나(`MeasurementSource`)가
추가됐다:

- `RawValue`/`SourceUnit`: 기준 길이(m) - Vertical Area/Parapet은 항상 미터로 정규화된 값을 담는다
  (Length/Area처럼 도면 원본 단위를 담지 않는다 - 여기서는 "기준 길이"라는 하나의 값만 있고, 그
  값의 원본 단위는 Source에 따라 다르므로 미터로 통일한다).
- `CalculationExpression`: 사람이 읽는 산식 전체. Vertical Area는 `"255.941 m × 0.100 m = 25.594 m²"`
  형태, Parapet은 측면/상부/합계 세 줄을 개행으로 구분한다.
- `MeasurementSource`: `CadSelection`/`ExistingMeasurement`/`Manual` 중 하나(문자열) - 이 값이 어떤
  경로로 나왔는지 구분한다.
- `ObjectHandles`: Source가 CAD 유래일 때만 채워진다. Manual이면 빈 목록.

높이/상부 폭의 원본 값(raw+unit)은 QuantityRecord에 별도 필드로 저장하지 않는다 -
`CalculationExpression`에 이미 사람이 읽을 수 있는 형태로 전부 들어있고, 별도 구조화 필드를 추가하면
Length/Area에는 없는 필드가 Vertical Area/Parapet에만 계속 늘어나 모델이 비대해진다. 자동 재계산이
아니라 "근거 확인"이 이번 Milestone의 목표이므로 이 정도로 충분하다고 판단했다 - 더 정밀한 재검산이
필요해지면(Milestone 6) 그때 확장한다.

## 향후 확장 후보 (이번에 구현하지 않음)

- **독립 내측/외측 Parapet 길이** - Advanced Parapet으로 별도 후보
- **개구부 공제** - 창/문 등
- **다구간(multi-segment) 높이** - 예: `10m×1m + 5m×0.5m`
- **Project/Drawing Unit Override** - Milestone 3에서 이미 보류, 필요해지면 Length/Area/Vertical
  Area/Parapet 전체에 한 번에 적용
