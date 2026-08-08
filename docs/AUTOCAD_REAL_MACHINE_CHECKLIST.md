# Real AutoCAD Machine Checklist

Level 1(Unit)/Level 2(Headless Integration) 테스트로 커버할 수 없는, 실제 AutoCAD가 정상 동작하는 머신에서만 확인 가능한 항목을 여기에 누적한다 (`docs/TESTING_WITHOUT_AUTOCAD.md` 참고). 이 문서의 항목이 Pending이라는 이유로 Milestone을 미완료 처리하지 않는다 - 단, Pending 상태는 명확히 남긴다.

**현재 상태: 전부 Pending.** 이 프로젝트의 개발 PC는 AutoCAD GUI 구동 시 그래픽 드라이버가 불안정해지는 문제가 있어(Milestone 1에서 확인, `docs/AUTOCAD_INTEGRATION.md` §8) 아직 한 항목도 실제로 검증하지 못했다.

## Milestone 1 — AutoCAD Connection

- [ ] Desktop만 실행 (AutoCAD 미실행) → "AutoCAD Not Running", 크래시 없음 (**이 항목만 이 PC에서 UI Automation으로 검증 완료**)
- [ ] AutoCAD 실행, Plugin NETLOAD 전 → "AutoCAD Detected · Plugin Not Loaded"
- [ ] NETLOAD 후 → 자동으로 "Connected" 전환
- [ ] DWG를 열고 → Desktop에 Drawing Name/Path/Layout/Unit이 정확히 표시
- [ ] 다른 DWG로 전환 → Desktop이 heartbeat 주기(2초) 안에 갱신
- [ ] Model → Layout1 전환 → Layout 표시 갱신
- [ ] AutoCAD 종료 → Desktop 크래시 없이 Disconnected/NoAutoCadProcess로 전환
- [ ] AutoCAD 재실행 + NETLOAD → Desktop이 자동으로 재연결
- [ ] AutoCAD 두 개 실행 → `AvailableInstances`에 둘 다 잡힘, `SelectInstanceAsync`로 전환 가능 (UI 셀렉터가 아직 없어 코드로 직접 호출하거나 후속 작업에서 UI 추가 필요)
- [ ] Unitless(단위 미지정) 도면 → "Unitless"로 정확히 표시, mm로 잘못 추정하지 않음
- [ ] AutoCAD PAN/ZOOM/PLINE 등 조작 중 Desktop의 heartbeat/polling 때문에 끊김이 느껴지지 않음

## Milestone 2 — Length

- [ ] `CWA_LENGTH`(또는 Desktop의 "CAD에서 객체 선택") 실행 → AutoCAD가 실제로 선택 프롬프트를 보여줌
- [ ] Polyline 길이 - `GetDistanceAtParameter(EndParam) - GetDistanceAtParameter(StartParam)`가 실제 Polyline 길이와 일치하는지 AutoCAD의 `LIST`/`AREA` 명령 결과와 대조
- [ ] Arc가 포함된 Polyline (직선 구간만 있는 것과 호 구간이 섞인 것 모두) - 좌표 직선거리 합산이 아니라 실제 호 길이로 계산되는지 확인 (§16 경고 사항)
- [ ] 순수 Line 객체
- [ ] 순수 Arc 객체
- [ ] Polyline2d / Polyline3d(레거시 폴리라인) - 최신 Polyline(LWPOLYLINE)과 동일하게 처리되는지
- [ ] 여러 객체 혼합 선택 (Line + Polyline + Arc)
- [ ] Esc로 선택 취소 → "선택이 취소되었습니다" (빨간 오류 아님)
- [ ] 지원하지 않는 객체(Hatch, Text, Block 등) 섞어서 선택 → 제외 개수/타입이 정확히 표시, 프로그램이 죽지 않음
- [ ] Unitless 도면에서 길이 산출 → "도면 단위가 설정되어 있지 않습니다", 자동 변환 안 함
- [ ] DWG를 전환하면서 연속으로 길이 산출 - 이전 결과가 남아있지 않고 새 도면 기준으로 계산되는지
- [ ] 100개 이상 객체 선택 - AutoCAD/Desktop 둘 다 멈추지 않는지, 응답 시간이 체감상 즉각적인지
- [ ] 길이 산출 도중/직후 AutoCAD에서 PAN/ZOOM - 끊김 없는지
- [ ] `DocumentLock`이 실제로 필요한 범위인지 - 다른 명령을 실행 중일 때 길이 산출을 시도하면 어떻게 되는지 (Lock 충돌 시 사용자에게 적절한 메시지가 가는지)
- [ ] "산출내역 추가"로 저장한 값을 재부팅/재시작 후에도 - **주의: 현재는 SQLite 영속화가 없다(Milestone 4에서 구현 예정), 프로그램을 재시작하면 산출내역이 사라지는 것이 정상이다.** 이 항목은 Milestone 4 이후로 재검토.

## Milestone 3 — Area

- [ ] Closed Polyline 1개 선택 → `AREA` 명령 결과와 `SelectAreaObjects` 응답의 `RawArea`가 일치
- [ ] Closed Polyline 여러 개 선택 → 합산값이 각 `AREA` 결과의 합과 일치
- [ ] Open Polyline 선택 → `IsClosed=false`, 0 m²가 아니라 Open으로 제외되고 "열린 형상 N개" 배너 표시
- [ ] 닫힘/열림 혼재 선택 → PartialSuccess, 유효한 것만 합산 + 제외 개수 정확히 표시
- [ ] Arc 구간이 포함된 닫힌 Polyline → `Curve.Area`가 호 구간까지 반영해 정확한 면적을 반환하는지 `AREA` 명령과 대조
- [ ] 자기교차(self-intersecting) Polyline → 예외 없이 값을 반환하는지, 반환한다면 그 값이 사용자 기대와 얼마나 다른지 (§18) - AutoCAD가 예외를 던지면 `InvalidGeometry`로 정상 제외되는지도 확인
- [ ] 매우 큰 다각형(좌표 값이 크거나 정점이 많은 경우) - 정밀도 손실 없이 계산되는지
- [ ] Circle 선택 → `Curve.Area`가 원 면적 공식(πr²)과 일치
- [ ] Ellipse(전체 타원) 선택 → 닫힌 것으로 인식되고 정확한 면적 반환
- [ ] Ellipse 호(elliptical arc, 일부만 그린 타원) 선택 → `Closed=false`로 인식되어 Open 처리되는지
- [ ] Region 선택 → `Region.Area`가 정확한 값을 반환
- [ ] Unitless 도면 → "도면 단위가 설정되어 있지 않습니다", 자동 변환 안 함
- [ ] mm 도면 / m 도면 각각에서 변환된 m² 값이 정확한지
- [ ] 지원하지 않는 객체(Hatch, Text, Line, Arc, Polyline3d, Dimension, BlockReference 등) 섞어서 선택 → 제외 개수/타입이 정확히 표시, 프로그램이 죽지 않음
- [ ] Esc로 선택 취소 → "선택이 취소되었습니다" (빨간 오류 아님)
- [ ] 100개 이상 선택 → AutoCAD/Desktop 둘 다 멈추지 않는지
- [ ] DWG 전환 후 연속으로 면적 산출 - 이전 결과가 남아있지 않고 새 도면 기준으로 계산되는지
- [ ] Layout 전환 후 면적 산출 - Model/Layout 어느 쪽에서 선택하든 동일하게 동작하는지
- [ ] 면적 산출 도중/직후 AutoCAD에서 PAN/ZOOM - 끊김 없는지
- [ ] `Hatch`를 실제로 지원해야 할 만큼 사용 빈도가 높은지 실사용자 피드백으로 재평가 (현재는 의도적으로 Unsupported, `docs/AUTOCAD_INTEGRATION.md` §5.6)
- [ ] `Polyline3d`를 면적 계산에 쓰려는 실사용 요구가 있는지, 있다면 어떤 평면 투영 규칙을 기대하는지 재평가

## Milestone 4 — Vertical Area + Parapet

새 AutoCAD API를 쓰지 않는다 - 기준 길이는 Milestone 2의 `SelectLengthObjects`를 그대로 재사용한다
(`docs/QUANTITY_COMPOSITION.md`). 그래서 이 Milestone의 Real AutoCAD 의존성은 낮다 - 아래 항목은
전부 "Length 획득 통합"과 "화면 표시" 확인이지, 새로운 AutoCAD Managed API 동작 확인이 아니다.

- [ ] CAD에서 기준선 선택 → Vertical Area로 넘어가 면적 계산 (Line/Polyline 각각)
- [ ] 여러 객체 선택 후 합산된 길이를 기준으로 Vertical Area 계산
- [ ] Esc로 선택 취소 → "선택이 취소되었습니다" (빨간 오류 아님)
- [ ] mm 도면 / m 도면 각각에서 기준 길이가 정확히 반영되는지
- [ ] Unitless 도면에서 CAD 선택 → "기준 길이 단위를 확인할 수 없습니다" 안내, 자동 진행 안 함
- [ ] Length 도구에서 실제로 측정한 뒤 Vertical Area/Parapet에서 "최근 측정값 사용" 선택 → 정확한 값 재사용 (Simulation Mode에서 발견한 PropertyChanged 버그가 실제 AutoCAD 환경에서도 고쳐졌는지 재확인)
- [ ] 파라펫 둘레 선택 → 한 면/양면/상부면 조합별 계산 확인
- [ ] 높이/상부 폭 단위(mm/cm/m) 조합별 계산 확인
- [ ] "산출내역 추가" → Dashboard에 정확한 값/산식으로 반영

## Milestone 5 — Drawing Navigation + Layer Isolation + Selection + WBLOCK Export

이 Milestone은 이전 Milestone들과 달리 AutoCAD 의존성이 높다 - View 조작(Zoom)/인터랙티브 영역
선택/Entity Visible 변경/Layer 상태 변경/WBLOCK 전부 실제 AutoCAD 화면과 파일 시스템에서만 최종
검증할 수 있다 (`docs/DRAWING_NAVIGATION.md` 참고, 어떤 게 Headless로 이미 검증됐고 어떤 게 여기
남았는지 문서화되어 있다).

### Zoom (§21-25) — 가장 먼저 확인해야 할 항목

`DrawingZoomService`의 WCS→DCS 변환(Matrix3d.PlaneToWorld + Target 이동 + ViewTwist 회전 후 역행렬)은
표준적으로 알려진 기법을 따랐지만, 실제 AutoCAD 화면 없이는 단 한 번도 시각적으로 검증하지 못했다.

- [ ] Zoom Extents - 도면 전체가 화면에 딱 맞게(10% 여백 포함) 보이는지
- [ ] Zoom Selection - 선택한 객체들만 화면에 맞게 보이는지, 선택 밖 영역이 과도하게 잘리거나 남지 않는지
- [ ] 매우 큰 모델(좌표 범위가 넓은 도면)에서 Zoom Extents
- [ ] 음수 좌표를 포함한 도면에서 Zoom
- [ ] 여러 도면이 동시에 열려 있을 때 - 활성 도면에만 Zoom이 적용되는지
- [ ] Model Space ↔ Layout 전환 후 Zoom가 여전히 정확한지
- [ ] View가 회전(ViewTwist ≠ 0)되어 있거나 등각뷰(Isometric) 등 정면이 아닌 뷰에서 Zoom - `DrawingZoomService`가 이 경우까지 고려해 구현했지만 실사용 시나리오(정면 2D 평면도)에서만 우선 검증
- [ ] 객체 1개(점 하나 크기)만 선택해서 Zoom Selection - 화면이 과도하게 확대되어 이상해지지 않는지 (최소 크기 보정 로직 확인)

### Selection (§26-31)

- [ ] Window 선택 - 영역 내부에 완전히 들어온 객체만 선택되는지
- [ ] Crossing 선택 - 영역에 닿기만 해도 포함되는지
- [ ] "첫 번째 모서리를 지정하세요" / "반대쪽 모서리를 지정하세요" 프롬프트가 자연스러운지, 고무줄 사각형이 정상적으로 그려지는지
- [ ] 혼합 객체 타입 선택 (Polyline/Line/MText/BlockReference/Dimension 등)
- [ ] 1개 객체, 100개 객체, 1,000개 객체 각각 선택
- [ ] Esc로 선택 취소 → "선택이 취소되었습니다" (오류 아님)
- [ ] 빈 영역 선택(아무 객체도 없는 곳) → 빈 목록으로 정상 처리되는지(AutoCAD 버전에 따라 PromptStatus가 다르게 올 수 있어 코드에서 이미 방어했지만 실기 확인 필요)

### Object Isolation (§32-36, §104)

- [ ] 선택 객체만 보기 → 나머지 객체가 실제로 화면에서 사라지는지
- [ ] 전체 복원 → 정확히 원래 보이던 객체만 다시 보이는지 (Isolation 이전에 이미 안 보이던 객체까지 보이게 만들지 않는지)
- [ ] Isolation 중 Entity.Visible 변경이 도면을 "수정됨" 상태로 표시하는지, 저장 확인 프롬프트가 뜨는지 - **뜬다면 사용자에게 어떻게 안내할지 결정 필요** (§104, 원본을 저장하지 않는다는 절대 원칙과 별개로 "수정됨 표시" 자체는 막지 못할 수 있다)
- [ ] Isolation 중 Ctrl+Z(Undo)를 누르면 어떻게 되는지 - Isolation이 Undo 스택에 몇 단계로 남는지
- [ ] Isolation 중 다른 객체가 이미 안 보이는(Off Layer 등) 상태였을 때 - 전체 복원 후에도 그 객체는 계속 안 보이는지
- [ ] Isolation 중 AutoCAD를 닫으면(Plugin unload) - 복원 안 된 상태로 종료될 때 사용자 도면에 어떤 흔적이 남는지 (§36)

### Layer (§37-48) — Restore 정확성이 가장 중요

- [ ] Layer On/Off 토글이 실제로 화면에 반영되는지
- [ ] "선택 Layer만 보기" - 선택 객체가 속한 Layer만 켜지고 나머지는 꺼지는지
- [ ] 전체 복원 - Isolation 전 상태(예: 원래 Off였던 Layer)가 정확히 그대로 복원되는지, "전부 On"으로 잘못 복원되지 않는지 (§45-46, 가장 위험한 부분)
- [ ] 현재 활성 Layer를 Off로 바꾸려는 시도 - `SetLayerVisibilityHandler`가 조용히 무시하는데, 실제 AutoCAD 동작(GUI로 직접 끌 때 나오는 경고 등)과 비교했을 때 사용자에게 혼란을 주지 않는지
- [ ] Locked Layer의 On/Off 토글 - Lock 상태와 무관하게 정상 동작하는지
- [ ] Xref가 포함된 도면의 Layer 목록/토글 동작
- [ ] DEFPOINTS 등 특수 Layer의 On/Off 동작에 이상이 없는지
- [ ] Layer가 매우 많은 도면(수백 개)에서 GetLayers 응답 시간
- [ ] Isolation 도중 사용자가 AutoCAD에서 직접 Layer를 변경 - Restore가 그 변경을 덮어쓰는지, 어떤 결과가 나오는지 (§48, 완벽한 conflict resolution은 범위 밖이지만 실제 동작은 관찰해서 문서화)
- [ ] Freeze/Thaw는 이번 Milestone에서 조회만 구현했다 - 실제 Frozen 상태가 `IsFrozen`에 정확히 반영되는지만 확인 (토글 기능 자체는 없음)

### WBLOCK Export (§49-63, §78) — 가장 위험도가 높은 항목

`Database.Wblock(ObjectIdCollection, Point3d)` + `SaveAs(path, DwgVersion.Current)`는 리플렉션으로
시그니처 존재만 확인했다 - 실제 출력 파일의 정확성/완전성은 전혀 검증하지 못했다.

- [ ] 단순 도형(Line/Polyline) Export → 새 DWG를 열어서 정확히 재현되는지
- [ ] Text/MText Export → 폰트/스타일이 원본과 동일하게 보존되는지
- [ ] BlockReference(단일/중첩 Block) Export → Block 정의가 함께 따라가는지, 중첩 Block도 정상인지
- [ ] Dimension Export → Dimension Style이 함께 따라가는지
- [ ] Hatch Export → 패턴/축척이 보존되는지
- [ ] 여러 Layer에 걸친 객체 Export → 대상 Layer들이 새 DWG에 전부 생성되는지, 원본 Layer 설정(색상/선종류)이 유지되는지
- [ ] Linetype이 Continuous가 아닌 객체 Export → Linetype 정의가 함께 따라가는지
- [ ] TextStyle/DimStyle이 표준이 아닌 객체 Export → 스타일 정의가 함께 따라가는지
- [ ] Xref를 포함한 선택 Export → 현재 범위 밖으로 명시했지만(§60) 실제로 어떻게 동작하는지(binding되는지, 깨지는지) 관찰 후 `docs/DRAWING_NAVIGATION.md`에 실제 동작 기록
- [ ] Export 후 원본 도면(현재 활성 Document)이 전혀 변경되지 않았는지 - Database 상태, Undo 스택, 수정 플래그 전부 확인
- [ ] Export한 새 DWG를 AutoCAD로 다시 열었을 때 오류/경고 없이 열리는지 ("도면 복구" 프롬프트가 뜨지 않는지)
- [ ] 대상 폴더에 쓰기 권한이 없는 경우 - 에러 메시지가 사용자에게 이해 가능한 형태로 오는지
- [ ] 같은 파일명이 이미 존재할 때 - SaveFileDialog의 덮어쓰기 확인이 정상 동작하는지(Desktop 쪽 기능이라 AutoCAD 재현 없이도 확인 가능하지만 전체 흐름에서 함께 확인)
- [ ] 1,000개 이상 객체 Export - 응답 시간, UI 멈춤 여부

## 검증 방법 메모

- 이 문서의 각 항목은 AutoCAD가 있는 머신에서 확인 후 `[ ]` → `[x]`로 바꾸고, 특이사항이 있으면 항목 옆에 메모를 남긴다.
- 새 Milestone에서 AutoCAD API를 새로 쓸 때마다(예: Milestone 3의 Area) 이 문서에 해당 섹션을 추가한다.
