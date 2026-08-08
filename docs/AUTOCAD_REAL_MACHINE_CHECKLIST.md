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

## 검증 방법 메모

- 이 문서의 각 항목은 AutoCAD가 있는 머신에서 확인 후 `[ ]` → `[x]`로 바꾸고, 특이사항이 있으면 항목 옆에 메모를 남긴다.
- 새 Milestone에서 AutoCAD API를 새로 쓸 때마다(예: Milestone 3의 Area) 이 문서에 해당 섹션을 추가한다.
