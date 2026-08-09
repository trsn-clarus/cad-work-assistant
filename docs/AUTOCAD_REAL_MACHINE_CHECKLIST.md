# Real AutoCAD Machine Checklist

Level 1(Unit)/Level 2(Headless Integration) 테스트로 커버할 수 없는, 실제 AutoCAD가 정상 동작하는 머신에서만 확인 가능한 항목을 여기에 누적한다 (`docs/TESTING_WITHOUT_AUTOCAD.md` 참고). 이 문서의 항목이 BLOCKED라는 이유로 Milestone을 미완료 처리하지 않는다 - 단, 상태는 명확히 남긴다.

## 상태 표기

각 항목 앞에 실제 실행 결과를 표기한다. 이 문서를 채우는 세션은 상태를 정직하게 남긴다 - 실행하지
않은 항목을 PASS로 표기하지 않는다.

| 표기 | 의미 |
| --- | --- |
| `[ ]` | 아직 시도하지 않음(기본값) |
| `[BLOCKED]` | 시도하려 했으나 머신/환경 제약으로 실행 자체가 불가능했음 |
| `[PASS]` | 실제 AutoCAD에서 실행해 기대한 대로 동작함을 확인 |
| `[FAIL]` | 실제 AutoCAD에서 실행했지만 기대와 다르게 동작함 - 원인/후속 조치를 항목 옆에 남긴다 |
| `[N/A]` | 이 프로젝트의 현재 범위에서 해당 없음(예: 아직 구현 안 한 기능) |

**현재 상태: 전부 `[BLOCKED]`.** 이 프로젝트의 개발 PC는 AutoCAD GUI 구동 시 그래픽 드라이버가
불안정해지는 문제가 있어(Milestone 1에서 확인, `docs/AUTOCAD_INTEGRATION.md` §8) 아직 한 항목도
실제로 검증하지 못했다. Milestone 8.5(2026-08-09)에서 다시 한번 이 PC에서 시도할지 검토했으나,
이 PC 외에 접근 가능한 안정적인 AutoCAD 머신이 없었고 사용자가 이 PC에서 그래픽 드라이버 위험을
다시 감수하지 않기로 결정해 준비 작업(이 문서 정리, 검증용 DWG 사양, 보고서 템플릿)만 진행했다 -
`docs/REAL_AUTOCAD_VALIDATION_2024.md`가 실제 실행 결과를 기록할 자리다.

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

## Milestone 6 — SQLite Persistence + Project Management

Persistence 자체(SQLite 파일 I/O, 트랜잭션, 재시작 복원)는 `CADWorkAssistant.Persistence.Tests`가
AutoCAD 없이 이미 완전히 검증한다. 여기 남는 건 "실제 AutoCAD에서 얻은 값"이 그 경로를 타는지뿐이다.

- [BLOCKED] 실제 AutoCAD 객체를 선택해 얻은 `QuantityRecord.ObjectHandles`가 Fake Handle이 아니라 진짜 AutoCAD Handle(예: `2A3`)로 저장되는지
- [BLOCKED] `SourceDrawing`이 실제 DWG의 파일명/경로 규칙대로 저장되는지 (전체 경로인지 파일명만인지 실제 값으로 확인)
- [BLOCKED] 실제 AutoCAD 세션에서 여러 번 측정 → "산출내역 추가" → Desktop 재시작 → 값 유지 확인
- [BLOCKED] 프로젝트 전환 중 AutoCAD 연결이 유지되는지 (Project Dialog가 AutoCAD 선택 흐름을 방해하지 않는지)

## Milestone 7 — Quantity History + Verification + Review

Verification 규칙 자체(Core.Verification, 9종 Rule)는 AutoCAD 없이 Core.Tests가 완전히 검증한다.
여기 남는 건 "실제 AutoCAD 값"이 검산에서 기대대로 동작하는지뿐이다.

- [BLOCKED] 실제 AutoCAD Line/Polyline으로 측정한 Length 기록이 Verification에서 Pass로 나오는지 (Raw-Converted Consistency 등 결정적 규칙이 실제 부동소수점 값에서도 통과하는지)
- [BLOCKED] 동일한 실제 AutoCAD 객체(같은 Handle)로 두 번 측정 → 중복 경고(Duplicate Handles Rule)가 실제로 뜨는지
- [BLOCKED] History에서 실제 CAD 값을 검토(리뷰 메모 작성 + "검토 완료") → 재시작 후 유지

## Milestone 8 — Production Installer + Plugin Autoload

이번 Milestone(8.5)의 핵심 대상. Installer/Bundle의 파일 배치 자체는 `scripts/test-release.ps1`로
이미 검증했다(§ Installer Smoke Test) - 여기 남는 건 "AutoCAD가 실제로 그 Bundle을 읽어 자동
로드하는지"와 그 이후 전체 실사용 워크플로다.

### Plugin Autoload (가장 먼저 확인해야 할 항목)

- [BLOCKED] `CADWorkAssistant-Setup-<version>-x64.exe` 설치 → AutoCAD 2024 실행 → **NETLOAD 없이** Plugin 자동 로드
- [BLOCKED] Named Pipe Server가 Plugin 초기화 시점에 자동으로 뜨는지
- [BLOCKED] Desktop이 별도 조작 없이 AutoCAD instance를 탐지하는지
- [BLOCKED] Heartbeat(Ping)가 성공하고 Drawing Context를 수신하는지
- [BLOCKED] Desktop UI가 "Connected" 상태로 전환되는지
- [ ] (Autoload 실패 시에만) 진단 목적으로 NETLOAD - 이걸로 Autoload 테스트를 PASS 처리하지 않는다. 실패 원인(Bundle 위치/Manifest/RuntimeRequirements 버전 범위/ModuleName 경로/AutoCAD 보안 설정/Windows 파일 차단) 특정 후 수정

### Connection 시나리오

- [BLOCKED] Case A: AutoCAD 먼저 실행 → Desktop 실행 → 연결
- [BLOCKED] Case B: Desktop 먼저 실행 → AutoCAD 실행 → 연결
- [BLOCKED] Case C: 연결 상태에서 AutoCAD 종료 → Desktop이 크래시 없이 Disconnected로 전환
- [BLOCKED] Case D: AutoCAD 재실행 → Desktop이 자동으로 재연결
- [BLOCKED] AutoCAD 인스턴스 2개 실행 → `AvailableInstances`에 둘 다 잡히는지 (UI 셀렉터는 범위 밖, 서비스 동작만 확인)

### Focus / Desktop UX (Simulation Mode로는 검증 불가능했던 항목)

- [BLOCKED] Desktop에서 "CAD에서 객체 선택" 클릭 → AutoCAD 창이 적절히 foreground로 전환되는지
- [BLOCKED] 선택 완료 후 Desktop에 결과가 뜨지만 AutoCAD 사용을 방해하는 강제 focus steal이 없는지
- [BLOCKED] SaveFileDialog(WBLOCK Export) 등이 실제 multi-window 환경에서 AutoCAD 뒤로 숨지 않는지
- [BLOCKED] AutoCAD가 PLINE/MOVE/PAN/ZOOM 등 다른 명령 실행 중일 때 Desktop 요청을 보내면 명령을 방해하지 않고 일관되게 처리되는지 (Lock 충돌 메시지 등)
- [BLOCKED] CAD selection 중 Esc가 AutoCAD 명령 상태와 Desktop 워크플로 양쪽 다 정상 복귀시키는지

### 한글 경로/파일명

- [BLOCKED] 한글 파일명(예: `학교_실내마감표.dwg`)으로 WBLOCK Export
- [BLOCKED] 한글 Layer 이름 조회/검색
- [BLOCKED] 한글이 포함된 Windows 사용자 계정(`C:\Users\<한글이름>\...`)에서 LocalAppData 경로 처리 - 이 개발 PC 자체가 이미 한글 경로(`바탕 화면`)를 포함하고 있어 어느 정도는 소스 레벨에서 매 빌드마다 검증되고 있지만, 실제 설치본 기준으로는 미확인

### 성능/리소스

- [BLOCKED] 연결 상태에서 Desktop/Plugin이 유휴 상태일 때 불필요하게 CPU를 지속 점유하지 않는지(작업 관리자로 육안 확인 수준)
- [BLOCKED] 기본 워크플로(측정→저장→검산)를 장시간 반복 후 메모리 증가가 뚜렷하지 않은지

### Security

- [BLOCKED] AutoCAD Plugin 보안/신뢰 위치 경고가 뜨는지, 뜬다면 매 실행마다 승인해야 하는 수준인지(Production UX blocker 여부 판단) - Installer가 Autodesk 보안 설정을 무단으로 낮추는 우회는 하지 않는다는 원칙 유지

## Milestone 11 — AutoCAD Plot + Drawing PDF Output

Core/IPC/Handler는 실제 AutoCAD 2024 DLL을 리플렉션으로 전량 검증했고(`docs/AUTOCAD_INTEGRATION.md`
§5.7), FakeAutoCad+Simulation Mode로 IPC/파일 배관 전체를 종단간 검증했다(`docs/
DRAWING_PDF_OUTPUT.md` §10). 여기 남는 건 "실제 AutoCAD Plot 엔진이 정확한 결과물을 만드는가"뿐이다.

### Capability 조회

- [BLOCKED] `GetPlotCapabilities`가 실제 설치된 Plot 장치 목록을 정확히 반환하는지(`DWG To PDF.pc3`
  포함 여부, 프린터 드라이버가 실제로 있을 때)
- [BLOCKED] `PlotConfigManager.SetCurrentConfig`로 장치를 순회 조회한 뒤 원래 장치로 복원하는 것이
  실제 Plot 대화상자의 기본 장치 선택에 영향을 주지 않는지
- [BLOCKED] 실제 장치의 `GetMediaBounds` 반환값 단위가 정말 mm인지(`CadPlotMediaDto.WidthMm/
  HeightMm`이 이 가정 위에 만들어져 있다) - 실제 출력물 실측과 대조
- [BLOCKED] CTB 도면과 STB 도면 각각에서 `Database.PlotStyleMode`가 문서와 일치하는 값을 주는지
- [BLOCKED] Layout이 여러 개인 도면에서 `IsCurrent`/`IsModel` 판정이 정확한지

### Window Plot (`AcquirePlotWindow`)

- [BLOCKED] 회전되지 않은 표준 UCS/View에서 두 점 지정 → `PlotDrawingPdf`로 넘긴 영역이 실제로
  선택한 영역과 일치하는 PDF가 나오는지
- [BLOCKED] 회전된 UCS 상태에서 두 점 지정 → 좌표가 왜곡 없이 정확한지(§4의 알려진 미검증 항목)
- [BLOCKED] Pan/Zoom 후 두 점 지정 → 화면 위치와 무관하게 WCS 기준으로 정확한지
- [BLOCKED] Esc로 취소 → `SelectionCancelled`로 정상 처리되고 AutoCAD 명령 상태가 깨끗하게 복귀하는지

### Plot 실행 정확성

- [BLOCKED] A4/A3 각각 선택 → 생성된 PDF의 실제 물리 페이지 크기가 정확히 210×297mm/297×420mm인지
  (PDF 리더의 "문서 속성"으로 실측)
- [BLOCKED] 자동/세로/가로 방향 각각 → 실제 출력 방향이 의도와 일치하는지, `PlotRotation` 매핑이
  옳은지
- [BLOCKED] 컬러(기존 설정 유지) → 원본 도면 색상이 그대로 출력되는지
- [BLOCKED] 흑백(monochrome.ctb) → 실제로 흑백톤으로 출력되는지, 선 굵기/글자 가독성이 유지되는지
- [BLOCKED] STB 도면에서 흑백 사전 설정이 항상 Unavailable로 막히는 것이 실제로 옳은 동작인지(STB
  환경에서 진짜로 monochrome 대응 방법이 없는지 재확인)
- [BLOCKED] Current Layout Scope - Layout에 이미 설정된 Page Setup/뷰포트가 그대로 반영되는지
- [BLOCKED] Model Window Scope - `PlotType.Window`+`SetPlotWindowArea`가 지정한 영역만 정확히
  잘라 출력하는지
- [BLOCKED] 원본 Layout의 PlotSettings가 Plot 전후로 전혀 변경되지 않았는지(CLAUDE.md 절대 원칙 1) -
  `PLOT` 명령을 GUI에서 직접 실행했을 때와 설정이 동일한지 대조
- [BLOCKED] Plot 중 DWG의 "수정됨" 플래그/저장 확인 프롬프트에 영향이 있는지
- [BLOCKED] 이미 다른 Plot이 진행 중일 때(`ProcessPlotState`) 두 번째 요청이 안전하게 Busy로
  거부되는지
- [BLOCKED] `PlotFactory.CreatePublishEngine()`으로 만든 Begin/End 시퀀스가 실제로 진행률/취소
  콜백 없이도 문제없이 끝까지 도는지(현재 구현은 `PlotProgress`에 `null`을 넘긴다)
- [BLOCKED] 매우 복잡한 도면(객체 수가 많거나 Hatch/Raster 포함)에서 Plot 소요 시간과 결과 정확성

## Milestone 12 — Text Tools

Core/IPC/Handler는 실제 AutoCAD 2024 DLL을 리플렉션으로 전량 검증했고(`docs/AUTOCAD_INTEGRATION.md`
§5.8), FakeAutoCad+Simulation Mode로 IPC/검증/배치 patch 의미론/UI를 종단간 검증했다(`docs/
TEXT_TOOLS.md` §12). 여기 남는 건 "실제 AutoCAD가 정확히 렌더링/기록하는가"뿐이다.

### 조회 (Select/Inspect)

- [BLOCKED] Dimension/MLeader/Table/AttributeReference가 섞인 선택에서 실제로 전부 제외되고, 제외된
  타입 이름이 정확히 표시되는지
- [BLOCKED] 실제 도면의 TextStyle이 커스텀 폰트/기울임(Oblique)/폭 비율(WidthFactor)을 쓸 때도
  `CadTextObjectDto`가 정확한 값을 반환하는지
- [BLOCKED] 실제 Annotative 문자에서 `Entity.Annotative == True`가 기대대로 판정되는지
- [BLOCKED] 서식이 있는 실제 MText(굵게/색상 부분 적용 등)에서 `HasInlineFormatting` 판정이
  실사용 서식 조합 전반에서 정확한지(현재는 `Contents != Text` 단순 비교)

### 편집 (Update, 배치 포함)

- [BLOCKED] 실제 DWG에서 배치 Height/Color/Layer 변경 후 화면에 즉시 정확히 반영되는지(재계산/
  재생성 없이)
- [BLOCKED] 색상을 ByLayer로 되돌린 문자가 실제로 Layer 색상을 따라가는지(다른 Layer로 옮긴 뒤
  에도)
- [BLOCKED] 잠긴 Layer의 문자를 배치에 포함했을 때 실제로 아무것도 바뀌지 않고(all-or-nothing)
  AutoCAD 쪽에도 부분 변경 흔적이 없는지
- [BLOCKED] 여러 속성을 한 번에 바꾼 배치 수정이 실제 Undo 스택에서 **한 번의 Ctrl+Z**로 전부
  되돌려지는지(Editor에 별도 Undo Mark API가 없다는 리플렉션 결론이 실제로 맞는지의 최종 확인)
- [BLOCKED] 배치 수정 중간에 AutoCAD가 다른 이유로 예외를 던졌을 때도 실제로 부분 수정이 전혀
  남지 않는지
- [BLOCKED] 문자 내용을 수정한 직후 DWG의 "수정됨(DBMOD)" 플래그가 정확히 서고, 저장하지 않고
  닫을 때 저장 확인 프롬프트가 뜨는지(CLAUDE.md 절대 원칙 1과 직결)

### 작성 (Create)

- [BLOCKED] `AcquireTextInsertionPoint`로 지정한 점이 회전된 UCS/Pan/Zoom 상태에서도 실제 클릭
  위치와 정확히 일치하는지(Milestone 11 §Window Plot에서 확인 못 한 것과 같은 종류의 위험)
- [BLOCKED] 새로 만든 DBText/MText가 현재 Layer의 기본 TextStyle/글꼴로 실제로 렌더링되는지
- [BLOCKED] Paper Space Layout이 활성 상태일 때 `Database.CurrentSpaceId`가 실제로 Paper Space를
  가리켜 문자가 올바른 공간에 생성되는지
- [BLOCKED] 명시한 색상(ByBlock/특정 ACI)이 실제 화면에서 정확한 색으로 보이는지, 특히 색상 7이
  배경(검정/흰 배경)에 따라 다르게 렌더링되는지(§29에서 예상한 White/Black 양면성)
- [BLOCKED] MText로 여러 줄 내용을 작성했을 때 자동 줄바꿈/폭 처리가 v1의 단순 구현으로도 실무에
  허용 가능한 수준인지

## 검증 방법 메모

- 이 문서의 각 항목은 AutoCAD가 있는 머신에서 실제로 시도한 뒤 앞머리의 상태 표기를 `[BLOCKED]`에서 `[PASS]`/`[FAIL]`/`[N/A]`로 바꾸고, 특이사항(재현 조건, AutoCAD 버전, 관련 커밋)을 항목 옆에 메모로 남긴다. 실행하지 않았는데 결과를 추측해서 적지 않는다.
- 새 Milestone에서 AutoCAD API를 새로 쓸 때마다(예: Milestone 3의 Area) 이 문서에 해당 섹션을 추가한다.
- 실제 검증을 실행한 세션은 결과를 이 문서에만 남기지 말고 `docs/REAL_AUTOCAD_VALIDATION_2024.md`에도 환경 정보와 함께 정리한다 - 이 문서는 "무엇을 확인해야 하는가"의 목록이고, 그 문서는 "언제 누가 무엇을 확인했는가"의 기록이다.
