# Validation DWG Specification

이 폴더는 `docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`/`docs/REAL_AUTOCAD_VALIDATION_2024.md` 실행에 쓸
검증용 DWG를 담는 자리다. **DWG 바이너리 자체는 AutoCAD 없이는 만들 수 없어(이 프로젝트에 DWG
Writer가 없다 - `CADWorkAssistant.Documents`는 Excel/PDF/CSV export 전용이고 DWG를 쓰지 않는다)
이 문서만 준비되어 있다.** 실제 AutoCAD 2024가 있는 머신에서 이 사양대로 `CWA_Validation_Basic.dwg`를
직접 그려서 이 폴더에 두고 검증에 쓴다. `.gitignore`의 `samples/**/*.dwg` 규칙에 따라 실제 DWG
파일은 커밋되지 않는다 - 이 사양서만 Repository에 남는다.

## CWA_Validation_Basic.dwg

Length/Area 계산 로직이 실제로 맞닥뜨릴 기하 형태를 한 도면에 모아둔다. 도면 단위(`INSUNITS`)는
**mm**로 설정한다(별도로 **m** 단위 사본과 **Unitless** 사본도 하나씩 더 준비하면
`docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`의 단위별 항목까지 같은 도면으로 커버된다).

### 필수 포함 요소

| 그룹 | 요소 | 목적 |
| --- | --- | --- |
| Length 대상 | 순수 `Line` 1개 이상 | 기본 길이 산출 |
| | 순수 `Arc` 1개 | 기본 길이 산출 |
| | 직선 구간만 있는 `Polyline`(LWPOLYLINE) | 기본 Polyline 길이 |
| | **Arc 구간이 섞인 `Polyline`** | `docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`가 가장 강조하는 항목 - 좌표 직선거리 합산이 아니라 실제 호 길이가 나오는지 |
| | 레거시 `Polyline2d`/`Polyline3d` 1개씩 | 최신 LWPOLYLINE과 동일 처리되는지 |
| Area 대상 | `Circle` | πr² 대조 |
| | `Ellipse`(전체 타원) | 면적 대조 |
| | Ellipse 호(일부만 그린 타원) | Open으로 처리되는지 |
| | 단순 사각형 `Closed Polyline` | 기본 면적 |
| | **Arc 구간이 섞인 Closed Polyline** | Curve.Area가 호 구간까지 반영하는지 |
| | `Open Polyline`(닫히지 않음) | 0 m²가 아니라 Excluded로 처리되는지 |
| | 자기교차(self-intersecting) `Polyline` | AutoCAD가 실제로 뭘 반환하는지 관찰 |
| | `Region` | Region.Area 대조 |
| 미지원 확인용 | `Hatch` | 현재 정책상 Unsupported - UI 설명이 정확한지 |
| | `Text`, `MText` | Length/Area 선택에 섞였을 때 정상 제외되는지 |
| | `Dimension` | 위와 동일 |
| | `BlockReference`(단순 + 중첩 Block 1개씩) | WBLOCK/Selection 대상 |
| Layer 구성 | 최소 5개 Layer | Layer Manager 검증 |
| | 그중 1개는 처음부터 Off | Isolation Restore가 "원래 꺼져 있던 걸 계속 꺼둔 채" 복원하는지 |
| | 그중 1개는 Locked | Locked Layer On/Off 상호작용 |
| | 그중 1개는 현재(Current) Layer로 설정 | Current Layer Off 시도 시 동작 확인 |
| 배치 | 객체들을 2~3개의 서로 떨어진 그룹으로 배치 | Window/Crossing 선택 차이가 드러나도록 |
| 좌표 | 최소 1개 그룹은 음수 좌표 영역에 배치 | 음수 좌표 Zoom 확인 |
| 문자 | Layer 이름 중 최소 1개는 한글(예: `벽체`, `치수`) | 한글 Layer 검색/조회 |
| | Text/MText 내용 중 최소 1개는 한글 | 한글 처리 일반 확인 |

### 선택 포함 (여유가 있으면)

- `Hatch` 패턴 + 축척이 표준이 아닌 예시 (WBLOCK 시 패턴 보존 확인용)
- 표준이 아닌 `TextStyle`/`DimStyle` 적용 객체 (WBLOCK 시 스타일 정의 동반 확인용)
- `Xref` 삽입 (범위 밖이지만 실제 동작 관찰용 - 별도 파일로 준비: `CWA_Validation_Xref.dwg`)
- 100개 이상 객체를 한 영역에 반복 배치 (대량 선택 성능 확인용 - 별도 파일로 준비: `CWA_Validation_Bulk.dwg`)

### 별도 파일: 단위 변형

- `CWA_Validation_Basic_Meters.dwg` - 위와 동일한 도면을 `INSUNITS = m`으로만 다시 설정
- `CWA_Validation_Basic_Unitless.dwg` - 위와 동일한 도면을 `INSUNITS = Unitless`로 설정 (자동으로 mm를 가정하지 않는지 확인하는 게 목적이므로 이 파일이 특히 중요하다)

## 실제 업무 DWG (별도 항목, §21)

Synthetic DWG만으로는 실제 건축/현장 도면의 복잡성(수백 개 Layer, Xref, 비표준 스타일, 대량
객체)을 대표하지 못한다. 가능하면 실제 프로젝트에서 쓰이는 복잡한 DWG 1개를 **복사본**으로
준비해 Navigation/Selection/Measurement/Layer/Export를 검증한다. 민감한 실제 도면이므로 이
Repository에는 절대 commit하지 않는다 - 로컬에서만 사용하고 `docs/REAL_AUTOCAD_VALIDATION_2024.md`에는
결과만 기록한다(도면 자체나 그 안의 민감 정보는 기록하지 않는다).

## 원본 보호 절차

모든 검증은 **복사본**에서 수행한다. 검증 세션 시작 전 원본을 다른 폴더에 백업해두고, 세션 종료
후 다음을 확인한다:

- 원본 파일의 마지막 수정 시각이 변하지 않았는지
- Layer 상태(On/Off/Frozen/Locked)가 검증 전과 같은지
- Entity Visibility가 검증 전과 같은지
- AutoCAD가 "저장하시겠습니까" 프롬프트 없이 종료됐는지(자동 저장이 실제로 한 번도 안 걸렸는지)
