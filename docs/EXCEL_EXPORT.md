# Excel Quantity Export (Milestone 9)

## 1. 목표

저장된 `QuantityRecord`(+최신 검산 결과+검토 상태)를 실무 제출/검토/적산 워크플로에 바로 쓸 수
있는 Excel "수량산출서"로 내보낸다. Microsoft Excel/COM/Interop에 의존하지 않는다 - Excel이
설치되어 있지 않은 PC에서도 항상 성공해야 하고, 헤드리스로 테스트할 수 있어야 한다(§4).

## 2. 라이브러리 선택 — ClosedXML

| 후보 | 제외 이유 |
|---|---|
| `Microsoft.Office.Interop.Excel` (COM) | 실제 Excel 설치 + 프로세스 기동이 전제조건, 헤드리스 테스트 불가능, AutoCAD가 이미 떠 있는 실무 환경에서 또 다른 무거운 프로세스를 띄우는 리스크 |
| EPPlus | 최신 버전은 상업적 사용 시 유료 라이선스 필요(NonCommercial 조건) |
| **ClosedXML 0.105.1** (채택) | MIT 라이선스, 다운로드 수 많고 활발히 유지보수, OpenXML `.xlsx`를 파일 시스템에 직접 작성(Excel 설치 여부 무관) |

`CADWorkAssistant.Documents`만 ClosedXML을 참조한다 - `Core`와 `CADWorkAssistant.AutoCAD`는
여전히 이 패키지를 전혀 모른다(§8.8, 절대 원칙 3의 연장).

## 3. 아키텍처

```text
Desktop.ViewModels.ExcelExportViewModel
  ▼ (SaveFileDialog 경로 선택 후 ExportAsync)
Desktop.Services.QuantityExcelExportCoordinator (IQuantityExcelExportCoordinator)
  │ Persistence에서 Project+QuantityRecord를 새로 읽음(캐시된 화면 상태를 신뢰하지 않는다) +
  │ IQuantityVerificationCoordinator로 최신 Verification/Review 재사용
  ▼
CADWorkAssistant.Documents.Excel.QuantityWorkbookModelBuilder (ClosedXML 비의존, 순수 매핑)
  ▼ QuantityWorkbookModel / QuantityWorkbookRow
CADWorkAssistant.Documents.Excel.QuantityWorkbookBuilder (ClosedXML을 다루는 유일한 클래스)
  ▼
.xlsx (원자적 저장) + IProjectContextService.AddExcelExportRecordAsync (ExportRecord+ActivityRecord)
```

자세한 호출 구조와 다른 Milestone과의 관계는 [`ARCHITECTURE.md`](./ARCHITECTURE.md) §8.8 참고.
`QuantityWorkbookModel`/`QuantityWorkbookRow`는 ClosedXML을 전혀 참조하지 않는 순수 데이터
클래스다 - 향후 PDF Export(Milestone 10 후보)가 같은 모델을 재사용할 수 있도록 의도적으로
분리했다.

## 4. 내보내기 범위 (Export Scope)

| Scope | 대상 |
|---|---|
| `All` | 프로젝트의 모든 `QuantityRecord` |
| `VerifiedOnly` | `QuantityReviewStatus.Verified`인 레코드만 |

**필터링은 항상 사용자의 `QuantityReviewStatus` 기준이다 - 자동 `VerificationSeverity`로 걸러내지
않는다.** 사용자가 "검토 완료"로 표시했지만 자동 검산이 여전히 Error를 내고 있는 레코드는
`VerifiedOnly`에서도 그대로 노출되고, 검산 결과 컬럼도 Error 그대로 보여준다 - 자동 경고를
조용히 숨기지 않는다(`docs/QUANTITY_VERIFICATION.md` §4와 같은 원칙).

정렬은 항상 `CreatedAt` 오름차순 → 동률이면 `Id`(순서 보장) - DB 원본 조회 순서에 의존하지 않는
결정적 순서다.

## 5. 시트 구성

| # | 시트명 | 내용 |
|---|---|---|
| 1 | **수량산출서** | No./구분/내역/산출식/수량/단위/검산/검토/비고 - 실무 제출용 메인 표 |
| 2 | **산출근거** | 최종 수량 + 원본값/원본단위/변환단위 + 산출식 + 측정방법 + 원본 DWG + 객체 수 + 작성일 (옵션, `IncludeCalculationBasis`) |
| 3 | **검산내역** | 검산결과(글리프+라벨) + 검토상태 + 검산 세부(개별 Check 라인) + 검토메모 (옵션, `IncludeVerificationDetail`) |
| 4 | **프로젝트정보** | 프로젝트명/발주처/현장/설명/작성일/앱 버전 + 요약 카운트 |

`ExcelExportOptions.IncludeReviewNotes`는 검토메모 컬럼 내용을(시트 자체가 아니라) 채울지
결정하고, `IncludeSourceDrawing`은 산출근거 시트의 "원본 DWG" 컬럼을 채울지 결정한다 - 둘 다 꺼도
시트 자체는 그대로 있고 값만 "-"가 된다(레이아웃이 옵션에 따라 흔들리지 않는다).

각 시트는 헤더 행 고정(`FreezeRows`), `AutoFilter`, 긴 텍스트 컬럼 자동 줄바꿈(`WrapText`)을
적용한다.

## 6. 원본 DWG 파일명

`QuantityRecord.SourceDrawing`(전체 경로)에서 `Path.GetFileName`으로 파일명만 추출한다 - 전체
경로를 그대로 노출하지 않는다(로컬 폴더 구조가 제출 문서에 드러나지 않도록). `IncludeSourceDrawing`이
꺼져 있으면 이 추출조차 하지 않는다.

## 7. 정밀도(소수 자릿수) 정책

`Core.Models.QuantityTypeDisplay.DecimalPlaces`가 유일한 소스다 - Excel과 화면(`QuantityHistoryRow`)이
같은 정책을 공유한다.

| Type | 자릿수 | 비고 |
|---|---|---|
| Length | 3 | `LengthFormatter.DefaultDecimalPlaces`와 동일 |
| Area | 2 | `AreaFormatter.DefaultDecimalPlaces`와 동일 |
| VerticalArea | 3 | m² 단위지만 화면에서 이미 3자리로 표시해온 기존 관례(`docs/QUANTITY_COMPOSITION.md`)를 그대로 따른다 |
| Parapet | 3 | 위와 동일 |

Area가 2자리, Vertical Area/Parapet이 3자리로 서로 다른 것은 Milestone 9에서 새로 만든 불일치가
아니라 Milestone 4부터 있던 기존 화면 표시 관례를 그대로 옮긴 것이다 - Excel에서 "통일"하지
않았다(새로운 화면-문서 간 표기 불일치를 만들지 않기 위해).

숫자는 항상 **진짜 numeric cell**로 저장한다(`cell.Value = value` + `NumberFormat.Format`) -
텍스트로 포맷된 문자열을 셀에 넣지 않는다. 단위는 별도 컬럼("단위")에 문자열로 분리한다.

## 8. 검산/검토 표시

- 검산 글리프/라벨: `Core.Verification.VerificationSeverityDisplay` (`✓ 검산 완료` / `! 확인 필요` /
  `× 오류` / 검산 이력이 아예 없으면 `미검산`)
- 검토 상태 라벨: `Core.Models.QuantityReviewStatusDisplay` (`검토 완료` / `확인 필요` / `미검토`)
- 검산내역 시트의 "검산 세부"는 개별 Rule의 Check 결과를 줄바꿈으로 나열한다(`QuantityVerificationResult.Checks`)
- `RuleSetVersion`이 `QuantityVerificationService.CurrentRuleSetVersion`보다 낮으면(재검산 필요,
  Stale) 수량산출서 시트의 "비고" 컬럼에 "재검산 필요"를 표시한다

## 9. 계산식 표현

`QuantityRecord.CalculationExpression`(사람이 읽기 위한 문자열, 예: `"125,331.214 + 81,404.992 +
49,204.454 mm = 255.941 m"`)을 **일반 텍스트로만** 출력한다 - Excel 수식으로 파싱하거나
재계산하지 않는다. 검산에 실제로 쓰이는 것은 이 문자열이 아니라 `CalculationMetadataJson`
(`docs/QUANTITY_VERIFICATION.md` §7) - 여기서도 동일하게, 문자열은 표시 전용이다.

## 10. 보안 — 수식 주입 방지 (Formula Injection)

사용자가 자유롭게 입력하는 문자열(프로젝트명/발주처/현장/설명/검토메모)이 `=`, `+`, `-`, `@`로
시작하면 일부 스프레드시트 프로그램이 이를 수식으로 재해석해 실행할 수 있다는 것이 알려진 CSV/
Excel Export 보안 이슈다. `QuantityWorkbookBuilder`는 모든 사용자 입력을 `IXLCell.Value =` 또는
`.SetValue()`로만 쓰고, ClosedXML의 `FormulaA1`/`FormulaR1C1`을 어디에서도 호출하지 않는다.
ClosedXML은 일반 값 대입에서 OOXML `<f>`(수식) 요소를 만들지 않으므로, Excel이 파일을 열 때 이
값들을 수식으로 재해석하지 않는다.

이 성질은 추정이 아니라 **4개의 실제 위험 문자열**(`=CMD('/c calc')!A1`, `+SUM(1+1)`, `-2+3`,
`@SUM(1)`)을 검토메모에 넣어 저장 → 재오픈 → `cell.HasFormula == false` 및
`cell.DataType == XLDataType.Text`로 직접 검증했다
(`QuantityWorkbookBuilderTests.BuildAndSave_FormulaLikeUserText_StoredAsLiteralTextNotFormula`).

## 11. 원자적 저장 (Atomic Save)

`QuantityWorkbookBuilder.SaveAtomically`:

1. 대상 폴더에 임시 파일(`~cwa_{guid}.xlsx`)로 저장
2. 그 임시 파일을 다시 열어 최소 구조(시트 존재)를 검증
3. 검증을 통과했을 때만 `File.Move(..., overwrite: true)`로 원래 경로에 원자적 교체
4. 실패하면 임시 파일을 최선의 노력으로 삭제하고 원래 경로는 건드리지 않는다 - 대상 경로에
   깨진 `.xlsx`가 남지 않는다

`SaveFileDialog`의 `OverwritePrompt`는 사용자가 기존 파일을 덮어쓸지 미리 확인하지만, 그 이후의
실제 쓰기 자체도 이 절차를 통해 원자적으로 이뤄진다.

## 12. Excel 스타일

Design System(`design-system/MASTER.md`) 팔레트를 Excel 가독성 우선으로 옮겼다 - gradient/네온/
굵은 검정 테두리 없이 옅은 헤더 배경(`#EEF2F5`) + 얇은 테두리(`#CAD3DC`, Thin/Hair)만 쓴다. "TRSN
CLARUS" 브랜딩은 프로젝트정보 시트 하단에 작은 텍스트("Generated by CAD Work Assistant · TRSN
CLARUS")로만 남기고, 로고 이미지는 삽입하지 않는다.

인쇄 설정은 4개 시트 공통으로 A4 가로(Landscape) + 폭 1페이지 맞춤(`FitToPages(1, 0)`) + 헤더 행
반복 인쇄(`SetRowsToRepeatAtTop`) + 쪽번호 바닥글("Page &P / &N")을 적용한다.

## 13. 내보내기 이력

`ExportRecord`(Milestone 5부터 존재)에 `ExportType`(`DwgSelection` / `ExcelQuantity`) 필드를
추가했다(`Migration003AddExportType`, `user_version` 3) - 기존 WBLOCK 내보내기 호출부는 생성자
기본 인자(`DwgSelection`)로 무변경이다. `IProjectContextService.AddExcelExportRecordAsync`가
`ExportRecord`+`ActivityRecord`("수량산출서 Excel 저장")를 한 트랜잭션에 저장하고, Dashboard의
Activity Log `ObservableCollection`에도 즉시 반영한다(재시작·프로젝트 전환 없이 바로 보인다).

파일 저장이 먼저 성공한 뒤에만 이력을 기록한다 - 파일 쓰기가 실패하면 이력도 남기지 않고, 파일은
성공했는데 이력 저장만 실패하면 로그로만 남기고 사용자에게는 "내보내기 실패"로 보고하지 않는다
(파일은 실제로 만들어졌기 때문 - 기존 DWG Export/Milestone 5와 같은 순서 원칙).

## 14. 파일명

`<프로젝트명>_수량산출서_<yyyyMMdd>.xlsx`, 예: `서울의료원 옥상 방수공사_수량산출서_20260809.xlsx`.
프로젝트명의 파일 시스템 금지 문자 제거는 `Core.Drawing.ExportFileNameService.Sanitize`(Milestone
5부터 있던 로직)를 그대로 재사용한다 - 새 sanitizer를 만들지 않았다.

## 15. Milestone 8 UI 흐름과의 관계

Excel Export는 OUTPUT 그룹의 새 "Excel" 화면(Ctrl+E)에서 시작한다 - 범위 라디오(전체/검토
완료만) + 4개 포함 정보 체크박스 + 실시간 요약 텍스트("총 N건 · 검토 완료 X · 확인 필요 Y ·
검산 오류 Z") + `Microsoft.Win32.SaveFileDialog` + 성공/오류 인라인 상태. 상태 흐름
(`IsExporting`/`IsSuccess`/`IsError`)은 Milestone 5의 `ExportWorkflowViewModel`(DWG WBLOCK)과
같은 관례를 따른다.

## 16. 이번 범위에서 의도적으로 하지 않은 것

- **PDF Export** — `QuantityWorkbookModel`을 그대로 재사용할 수 있게 설계했지만 이번 Milestone
  범위 밖(Milestone 10 후보, `docs/ARCHITECTURE.md` §12)
- **Excel 안에 로고 이미지 삽입** — 텍스트 브랜딩만(§12)
- **검산 이력을 시트에 append** — `QuantityVerificationSnapshot`이 애초에 최신 1건만 upsert하므로
  (`docs/QUANTITY_VERIFICATION.md` §8) Excel도 최신 상태만 반영한다
- **사용자 정의 시트/컬럼 구성** — 4개 시트 구조는 코드로 고정, 향후 필요해지면 재평가
- **Excel 자체 수식/피벗/차트 생성** — 이 문서는 "산출 결과의 정적 스냅샷"이지 Excel에서 추가
  가공하는 것을 전제하지 않는다
