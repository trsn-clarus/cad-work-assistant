# Architecture

## 1. 배경 조사 결과

개발을 시작하기 전 실제 개발 환경을 조사했다 (2026-08-08 기준).

| 항목 | 결과 |
|---|---|
| 설치된 AutoCAD | AutoCAD 2024 (내부 버전 24.3) |
| AutoCAD 2024 .NET 런타임 | **.NET Framework 4.8** (acdbmgd.dll, acmgd.dll, accoremgd.dll 확인) |
| .NET SDK | 최초 미설치 → 개발 진행을 위해 .NET 8 SDK 설치 |
| Visual Studio | 미설치 (dotnet CLI + SDK 기반으로 개발) |
| Git | 2.54.0 설치됨 |

**핵심 제약**: AutoCAD 2024는 .NET Framework 4.8 기반 Managed API를 제공한다. AutoCAD 2025부터 Autodesk가 .NET 8 기반으로 전환했지만, 현재 사용자 PC에는 2024가 설치되어 있으므로 **AutoCAD Plugin(In-process DLL)은 net48을 타겟으로 해야 한다.** 반면 Desktop 애플리케이션은 AutoCAD와 별도 프로세스이므로 이 제약을 받지 않는다.

이 비대칭성이 전체 아키텍처를 결정하는 가장 중요한 요인이다.

## 2. 프로세스 구조 — Two-Process Model

```text
┌─────────────────────────────┐         Named Pipe (JSON)        ┌──────────────────────────────┐
│  CADWorkAssistant.exe        │ <───────────────────────────────> │  acad.exe                      │
│  (Desktop, net8.0-windows)   │                                    │   └─ CADWorkAssistant.AutoCAD  │
│                               │                                    │        .dll (net48, in-proc)   │
│  WPF UI / MVVM                │                                   │   - IExtensionApplication      │
│  Project / Quantity / Export  │                                   │   - CWA_* Commands             │
└───────────────┬───────────────┘                                   │   - Pipe Server                │
                │                                                    └──────────────┬────────────────┘
                │ 참조                                                              │ 참조
                ▼                                                                    ▼
        ┌───────────────────────────────────────────────────────────────────┐
        │                CADWorkAssistant.Core (netstandard2.0)                │
        │   순수 C# 계산/도메인 로직. AutoCAD API, WPF 어느 쪽에도 의존하지 않음  │
        └───────────────────────────────────────────────────────────────────┘
                │                                            │
                ▼                                            ▼
   CADWorkAssistant.Infrastructure                 CADWorkAssistant.Documents
   (Logging / Settings / SQLite, netstandard2.0)    (Excel / PDF Export, netstandard2.0)
```

**왜 두 프로세스로 분리하는가**

1. AutoCAD 2024의 net48 제약을 Desktop UI까지 끌고 가지 않기 위해서다. WPF 자체는 net48에서도 동작하지만, 최신 .NET(성능, 보안 패치, 장기 지원)을 UI에 쓰고 싶다면 in-process 플러그인 방식만으로는 net48에 묶인다.
2. AutoCAD가 실행되어 있지 않아도 Desktop App은 켜져서 프로젝트/산출내역을 확인할 수 있어야 한다 (§17 계산 이력, §19 프로젝트 관리는 AutoCAD 없이도 열람 가능해야 함).
3. AutoCAD 버전이 바뀌어도(2024→2025→2026) Plugin DLL만 버전별로 다시 빌드하면 되고, Desktop App과 Core 로직은 그대로 재사용된다 (§29).
4. 향후 AutoCAD가 crash/hang 하더라도 Desktop App과 저장된 산출내역 데이터는 영향받지 않는다 (안정성, 데이터 유실 방지 원칙).

**Trade-off**: In-process 단일 DLL 방식보다 구현이 복잡하다(IPC 계층 필요). 이 비용은 Core에 IPC 계약(요청/응답 DTO)을 정의하고 Named Pipe + `System.Text.Json` 직렬화라는 단순한 조합으로 최소화한다. gRPC, WCF 등 무거운 프레임워크는 사용하지 않는다.

## 3. 프로젝트 구성과 책임

| 프로젝트 | TFM | 책임 | AutoCAD/WPF 의존성 |
|---|---|---|---|
| `CADWorkAssistant.Core` | netstandard2.0 | 단위 변환, 길이/면적/파라펫 계산, 도메인 모델(Project, QuantityItem 등), IPC 요청/응답 DTO. **AutoCAD 타입을 절대 참조하지 않는다** → AutoCAD 없이 유닛 테스트 가능 (§32) | 없음 |
| `CADWorkAssistant.Infrastructure` | netstandard2.0 | 구조화 로깅(Serilog), 설정 저장(JSON), 향후 SQLite 데이터 접근 | 없음 |
| `CADWorkAssistant.Documents` | netstandard2.0 | Excel/PDF/CSV 내보내기 (Milestone: Excel Export 단계에서 실제 구현) | 없음 |
| `CADWorkAssistant.Desktop` | net8.0-windows | WPF UI, MVVM, Named Pipe Client, Project Manager 화면 | WPF |
| `CADWorkAssistant.AutoCAD` | net48 | AutoCAD Managed API 연동, CWA_* 명령, Named Pipe Server, 원본 DWG 보호/Undo 그룹 처리 | AutoCAD 2024 Managed API |
| `*.Tests` | net8.0 | Core/Infrastructure 로직 단위 테스트 | 없음 (AutoCAD 미설치 환경에서도 실행 가능) |

Core/Infrastructure/Documents가 `netstandard2.0`인 이유: net48(Plugin)과 net8.0-windows(Desktop) 양쪽에서 참조 가능한 가장 단순한 공통분모이기 때문이다. net8.0으로 멀티타게팅하는 방법도 있으나, Core 로직이 최신 BCL API를 필요로 하지 않는 한 빌드 구성을 두 배로 늘릴 이유가 없다 (§0 "불필요하게 복잡한 구조 지양").

## 4. AutoCAD 연동 계층

자세한 내용은 [AUTOCAD_INTEGRATION.md](./AUTOCAD_INTEGRATION.md) 참조. 핵심 원칙만 요약:

- **원본 보호**: 분석 전용 작업(길이/면적 조회 등)은 Transaction을 읽기 전용으로 열고 절대 `Database.SaveAs`/`qsave`를 자동 호출하지 않는다. 도면을 변경하는 작업(Export, Text 삽입 등)만 명시적 사용자 확인 후 진행한다 (§22).
- **Undo 그룹화**: AutoCAD 문서를 변경하는 모든 작업은 `Editor.StartUserInteraction` / Command 단위로 하나의 Undo 그룹으로 묶어 사용자가 AutoCAD에서 Ctrl+Z 한 번으로 되돌릴 수 있게 한다 (§23).
- **버전 독립성**: Plugin 프로젝트는 설치된 AutoCAD 경로를 자동 탐지(`AutoCAD.props`)하며, 특정 버전 경로를 하드코딩하지 않는다. 향후 AutoCAD 2025+(.NET 8) 지원이 필요해지면 `CADWorkAssistant.AutoCAD2025` 프로젝트를 추가하고 Core/Infrastructure는 그대로 재사용한다.

## 5. IPC 프로토콜 (Milestone 1에서 구현 예정)

- 전송: `System.IO.Pipes.NamedPipeServerStream` (AutoCAD Plugin이 서버) / `NamedPipeClientStream` (Desktop이 클라이언트)
- Pipe 이름: `CADWorkAssistant.{AutoCAD 프로세스 ID}` — AutoCAD 다중 인스턴스 지원을 위해 PID를 포함한다.
- 메시지 형식: 줄바꿈으로 구분된 JSON (`System.Text.Json`), 요청/응답 DTO는 `CADWorkAssistant.Core.Ipc` 네임스페이스에 정의해 Desktop/Plugin이 동일 타입을 공유한다.
- 연결 끊김/AutoCAD 미실행 감지: Desktop이 주기적으로 실행 중인 `acad.exe` 프로세스 목록을 조회하고, 각 프로세스에 대응하는 Pipe 존재 여부로 연결 상태를 판단한다.

## 6. Desktop App 구조 (MVVM)

- `*.xaml` — 뷰 (구조/레이아웃/스타일), `Themes/DesignTokens.xaml`에 색상·타이포·spacing 토큰 정의
- `ViewModels/` — 자체 구현 `ObservableObject`/`RelayCommand` 기반, 상태와 커맨드
- `Services/` — Pipe 통신, 파일 시스템 접근 등 View/ViewModel과 분리된 서비스 (Milestone 1에서 추가)
- Navigation은 §27 정보구조를 기반으로 하되 실제 구현은 [`design-system/MASTER.md`](../design-system/MASTER.md)의 PROJECT/CAD/QUANTITY/OUTPUT/SETTINGS 그룹을 따른다. Milestone 0의 UI Shell은 더미 데이터로 채워진 상태이며, 실제 AutoCAD 연동은 Milestone 1부터 연결한다.
- UI 디자인 원칙(색상, spacing, 밀도, 안티패턴)은 `design-system/MASTER.md`가 단일 소스다 — ARCHITECTURE.md는 프로세스/데이터 구조를, design-system은 시각적 규칙을 다룬다.

## 7. 로깅 / 설정

- **Logging**: Serilog, 파일 싱크. 경로: `%LOCALAPPDATA%\CADWorkAssistant\logs\yyyy-MM-dd.log`, 일 단위 롤링. 도면 내부 좌표/치수 등 민감할 수 있는 상세 데이터는 Verbose 레벨에서만 기록하고 기본 레벨(Information)에는 요약만 남긴다 (§25).
- **Settings**: `%APPDATA%\CADWorkAssistant\settings.json`, `System.Text.Json` 직렬화. 소수점 자릿수(길이/면적 별도), 기본 단위 표시 등 사용자 환경설정을 저장한다 (§21).
- **Project/Quantity 데이터**: SQLite는 실제 Quantity Sheet 기능(Milestone 4) 구현 시점에 도입한다. 지금 시점에 스키마를 미리 설계하지 않는다 (§34 조기 구현 금지).

## 8. 의사결정 로그

| 결정 | 대안 | 선택 이유 |
|---|---|---|
| Desktop: net8.0-windows + WPF | net48 WPF (단일 프로세스) | 장기 지원, 최신 성능/보안 패치, AutoCAD 버전 제약에서 UI를 분리 |
| Plugin: net48 | net8.0 | 현재 설치된 AutoCAD 2024가 net48만 지원 |
| Core/Infra/Documents: netstandard2.0 | net8.0 멀티타게팅 | net48·net8 양쪽에서 참조 가능한 가장 단순한 공통분모 |
| MVVM: 자체 구현 (`ObservableObject`/`RelayCommand`, 30줄 내외) | CommunityToolkit.Mvvm, Prism | 이 규모의 앱에 필요한 MVVM 표면(`INotifyPropertyChanged`, `ICommand`)이 매우 작아 외부 의존성 없이 직접 구현. 필요 시점에 CommunityToolkit.Mvvm으로 교체 가능 (인터페이스 호환) |
| IPC: Named Pipe + JSON | gRPC, WCF, COM Automation | 의존성 최소, 방화벽/포트 이슈 없음, 로컬 프로세스 간 통신에 충분 |
| 로깅: Serilog | NLog, log4net | 구조화 로깅 표준, netstandard2.0 지원, 활발한 유지보수 |
| Test: xUnit | NUnit, MSTest | .NET 커뮤니티 표준, dotnet CLI 기본 템플릿과 궁합 |

## 9. 아직 결정하지 않은 것 (의도적으로 보류)

- SQLite 접근 방식(Raw ADO.NET vs Dapper vs EF Core) — Milestone 4 착수 시 결정
- Excel 라이브러리(ClosedXML 후보) — Excel Export 착수 시 라이선스/유지보수 재확인 후 결정
- PDF 라이브러리 — PDF Export 착수 시 결정 (QuestPDF는 회사 규모에 따라 상업 라이선스 필요할 수 있어 확인 필요)
- Installer(Inno Setup vs MSIX) — 첫 배포 가능한 빌드가 나온 뒤 결정
- AutoCAD 2025+(.NET 8) 지원 — 필요 시점에 별도 프로젝트로 추가
