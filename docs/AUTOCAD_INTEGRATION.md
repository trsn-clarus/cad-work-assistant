# AutoCAD Integration

## 1. 확인된 환경

- 설치 버전: AutoCAD 2024 (internal version 24.3.119.0), `C:\Program Files\Autodesk\AutoCAD 2024`
- Managed API 어셈블리: `acdbmgd.dll`, `acmgd.dll`, `accoremgd.dll` — **.NET Framework 4.8**
- AutoCAD 2025부터 Autodesk가 .NET 8 기반으로 전환했으나, 2024는 대상 아님. 따라서 `CADWorkAssistant.AutoCAD` 프로젝트는 `net48`을 타겟으로 한다.

## 2. Plugin 참조 방식

AutoCAD Managed API DLL은 **NuGet으로 배포되지 않고 로컬 설치 경로에서 직접 참조**한다 (Autodesk 공식 관행). `Private=False`/`CopyLocal=false`로 설정해 우리 설치 프로그램이 이 DLL들을 재배포하지 않도록 한다 — AutoCAD가 이미 제공하며, 라이선스상으로도 재배포 대상이 아니다.

경로는 하드코딩하지 않고 `src/CADWorkAssistant.AutoCAD/AutoCAD.props`에서 다음 우선순위로 탐지한다:

1. 환경변수 `AUTOCAD_INSTALL_DIR`
2. MSBuild 속성 `-p:AutoCADInstallDir=...`
3. `C:\Program Files\Autodesk\AutoCAD 2025\` → `2024\` → `2023\` 순으로 존재 여부 확인

세 방법 모두 실패하면 명확한 빌드 오류 메시지를 낸다 (§34 하드코딩 금지 원칙에 따라 "찾지 못하면 조용히 실패"가 아니라 "찾지 못하면 설정 방법을 알려주고 중단").

## 3. 로딩 방식

- **개발 중**: `NETLOAD` 명령으로 빌드된 DLL을 수동 로드
- **배포판**: `.bundle` 폴더 구조 + `PackageContents.xml` Autoloader 방식 채택 예정 (Milestone 1 이후 구체화). AutoCAD 시작 시 자동 로드되며, 설치 프로그램이 `%APPDATA%\Autodesk\ApplicationPlugins\` 아래에 배치한다.

## 4. 명령 이름 (CWA prefix)

| 명령 | 기능 | 도입 Milestone |
|---|---|---|
| `CWA` | 메인 Task Pane / 상태 확인 | 1 |
| `CWA_LENGTH` | 길이 산출 선택 모드 | 2 |
| `CWA_AREA` | 면적 산출 선택 모드 | 3 |
| `CWA_PARAPET` | 파라펫 계산 선택 모드 | 5 |
| `CWA_EXPORT` | 부분 DWG 추출 | 8 |
| `CWA_LAYER` | Layer 도구 | 9 |
| `CWA_PLOT` | Plot 프리셋 실행 | 10 |

`CWA` prefix는 흔한 AutoCAD 내장/서드파티 명령과 충돌 가능성이 낮아 채택했다. 실제 등록 전 `(command-list)` 또는 각 명령 등록 시 AutoCAD가 충돌을 알려주므로, Milestone 1에서 실제 등록하며 재확인한다.

## 5. IPC — Desktop ↔ Plugin

Desktop(net8.0, 별도 프로세스)과 Plugin(net48, `acad.exe` in-process)은 **Named Pipe**로 통신한다.

- 서버: Plugin (`NamedPipeServerStream`), AutoCAD 문서가 열려 있는 동안 대기
- 클라이언트: Desktop (`NamedPipeClientStream`)
- Pipe 이름: `CADWorkAssistant.{acad.exe PID}` — 다중 AutoCAD 인스턴스를 구분하기 위해 PID를 포함
- 메시지: 줄바꿈 구분 JSON, DTO는 `CADWorkAssistant.Core.Ipc`에 정의해 두 프로젝트가 동일 타입을 참조
- Desktop은 `Process.GetProcessesByName("acad")`로 실행 중인 AutoCAD 인스턴스 목록을 얻고, 각 PID에 대응하는 Pipe 연결을 시도해 연결 상태를 판정한다

이 프로토콜의 정확한 요청/응답 스키마는 Milestone 1에서 실제 "AutoCAD 연결 확인" 기능을 구현하며 확정한다 — 지금 시점에 미리 설계하지 않는다.

## 6. 원본 보호 / Undo 구현 방침

- **읽기 전용 작업** (길이/면적 조회 등): `Database.TransactionManager.StartTransaction()`을 읽기 용도로만 사용하고 `Commit()` 대신 `Dispose()`(자동 Abort)로 종료. `Database.SaveAs`/`qsave`는 어떤 경로로도 자동 호출하지 않는다.
- **변경 작업** (Text 삽입, Export 등): 실행 전 사용자 확인 UI를 거치고, `Editor.Command`/Transaction을 하나의 논리적 단위로 묶어 AutoCAD Undo 스택에 단일 항목으로 남도록 한다.
- 이 규칙은 코드 리뷰 체크리스트에도 반영한다 — Plugin 코드에서 `Commit()`을 호출하는 모든 지점은 "왜 도면을 변경해야 하는가"가 명확해야 한다.

## 7. 단위 처리

- 도면 단위는 `Database.Insunits`(`INSUNITS` 시스템 변수)로 조회한다. `UnitsValue.Millimeters`가 가장 흔하지만 하드코딩하지 않고 항상 조회한다.
- Length/Area는 AutoCAD 내부 단위(도면 단위) 그대로 가져와 `CADWorkAssistant.Core`의 변환 로직에 넘긴다 — 변환 책임은 Plugin이 아니라 Core에 둔다 (테스트 가능성, §32).

## 8. 향후 AutoCAD 버전 추가

새 버전(예: AutoCAD 2026, net8.0 기반) 지원이 필요해지면:

1. `src/CADWorkAssistant.AutoCAD2026/` 프로젝트 신규 생성 (다른 TFM 가능)
2. Command/IExtensionApplication 구현을 최대한 재사용하되, 버전별 API 차이는 각 프로젝트 안에서 흡수
3. `CADWorkAssistant.Core`/`Infrastructure`는 변경 없이 공유
4. Desktop App은 연결된 AutoCAD의 버전과 무관하게 동일한 IPC 프로토콜로 통신 (프로토콜 버전 필드를 두어 하위 호환 확인)

지금은 단일 버전(2024)만 지원하며, 이 구조는 실제로 두 번째 버전을 지원해야 하는 시점에 검증한다.
