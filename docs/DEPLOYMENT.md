# Deployment (Milestone 8)

이 문서는 CAD Work Assistant를 "설치해서 바로 쓸 수 있는 Windows 제품"으로 만드는 배포
아키텍처를 설명한다. 배포 절차 자체(release 커맨드, 체크리스트)는 `docs/RELEASE_CHECKLIST.md`를
본다. UI/UX 결정 근거는 `design-system/PRODUCTION_UI_REVIEW.md`를 본다.

## 1. Runtime Architecture

```text
Windows
  CADWorkAssistant.Desktop.exe   (.NET 8 WPF, self-contained win-x64)
        │
        │ Named Pipe (로컬 프로세스 간 통신, 유일한 network-like 의존성)
        ▼
  CADWorkAssistant.AutoCAD.dll   (.NET Framework 4.8, AutoCAD in-process plugin)
        │
        ▼
  AutoCAD 2024 (실기 검증 완료 버전, R24.3)

  CADWorkAssistant.Desktop.exe
        │
        ▼
  SQLite (%LOCALAPPDATA%\CADWorkAssistant\data\*.db)
```

Desktop과 Plugin은 별도 프로세스다(`docs/ARCHITECTURE.md` §5). Runtime에 인터넷 연결이 필요한
경로는 없다 - Telemetry/Analytics/Crash Upload/License Server/Update Server를 전부 의도적으로
넣지 않았다(§5, §153).

## 2. Version Source of Truth

`Directory.Build.props`의 `CwaVersion` 프로퍼티 하나가 모든 버전 값의 근원이다.

```text
Directory.Build.props (CwaVersion="0.9.0", ReleaseChannel="RC")
        │
        ├─ 모든 프로젝트의 AssemblyVersion/FileVersion/Product/Company (MSBuild가 자동 상속)
        ├─ scripts/build-release.ps1 -> Directory.Build.props를 파싱해 $version 결정
        │     ├─ installer/CADWorkAssistant.bundle/PackageContents.xml의 AppVersion을 정규식으로 교체
        │     └─ ISCC.exe /DAppVersion=$version /DReleaseChannel=$releaseChannel -> Setup 파일명(CADWorkAssistant-Setup-$version-$releaseChannel-x64.exe)
        └─ src/CADWorkAssistant.Desktop/ViewModels/SettingsViewModel.cs
              -> Assembly.GetExecutingAssembly().GetName().Version (런타임에 리플렉션으로 읽음, Settings 화면에 표시)
```

버전을 올릴 때는 `Directory.Build.props`의 `CwaVersion` 한 곳만 바꾸면 된다.

## 3. Desktop Packaging

- `dotnet publish -r win-x64 --self-contained true` - .NET Runtime을 사용자가 별도 설치할 필요가
  없다.
- `PublishTrimmed=false`, `PublishSingleFile=false` - WPF는 binding/reflection에 크게 의존해서
  Trimming이 조용히 무언가를 깨뜨릴 위험이 있고(`CLAUDE.md`의 반복되는 교훈: 컴파일 성공은 동작
  확인이 아니다), Single-file은 실익 대비 안정성 리스크가 커서 켜지 않았다(§110-111). Installer가
  하나의 실행 파일이면 되는 것이지, Desktop 자체가 단일 파일일 필요는 없다(§108).
- `RuntimeIdentifier`/`SelfContained`는 csproj 기본값으로 박아두지 않았다 - `dotnet build`/
  `dotnet run`(일상 개발 루프)이 매번 win-x64 런타임 팩을 복원하지 않도록, publish 시점에만
  `-r win-x64 --self-contained true`를 커맨드라인으로 넘긴다.
- 결과물은 `publish/desktop/`(gitignore 대상) - `CADWorkAssistant.Desktop.exe` + 의존 DLL +
  .NET Runtime 파일 약 200여 개.

## 4. AutoCAD Plugin Bundle

Autodesk의 공식 ApplicationPlugin bundle 구조를 그대로 따른다.

```text
installer/CADWorkAssistant.bundle/
  PackageContents.xml          (Manifest - 소스 관리 대상)
  Contents/Windows/
    CADWorkAssistant.AutoCAD.dll   (빌드 결과물 - gitignore 대상, build-release.ps1이 채움)
    CADWorkAssistant.Core.dll
    CADWorkAssistant.Infrastructure.dll
    Serilog*.dll
```

- `LoadOnAutoCADStartup="True"` - 설치 후 사용자는 NETLOAD를 직접 입력할 필요가 없다(§116).
- `SeriesMin="R24.3" SeriesMax="R24.3"` - 이 개발 PC에 실제 설치된 AutoCAD 2024의 내부 릴리스
  ID다. `C:\ProgramData\Autodesk\AutoCAD 2024\` 폴더명에서 직접 확인했다(추측 아님). 실기
  검증이 안 된 다른 AutoCAD 버전까지 지원한다고 주장하지 않는다(§114).
- Autodesk 호스트 DLL(`acdbmgd.dll`/`acmgd.dll`/`accoremgd.dll`)은 Bundle에 절대 포함하지
  않는다 - `CADWorkAssistant.AutoCAD.csproj`가 이 세 참조를 전부 `Private=false`로 선언하고
  있어 애초에 빌드 출력 폴더에 복사되지 않는다(§113, `scripts/audit-runtime.ps1`이 이걸 실제로
  재확인한다).
- 설치 위치: `%APPDATA%\Autodesk\ApplicationPlugins\CADWorkAssistant.bundle\` (사용자별,
  관리자 권한 불필요).
- AutoCAD가 설치되어 있지 않은 PC에서도 이 폴더 복사 자체는 실패하지 않는다 - 아무도 로드하지
  않을 뿐이다(§117). Desktop은 AutoCAD 없이도 Project/History/Verification 등 정상 동작한다.

## 5. Installer (Inno Setup)

`installer/CADWorkAssistant.iss`. Inno Setup 6.7.3 (winget: `JRSoftware.InnoSetup`)로 컴파일한다.

- **관리자 권한 불필요** (`PrivilegesRequired=lowest`) - AutoCAD ApplicationPlugins 폴더 자체가
  이미 사용자별이라, Desktop도 `{localappdata}\Programs\CAD Work Assistant\`에 설치하면 회사
  PC에서 관리자 권한이 없는 사용자도 전체를 설치할 수 있다(§132).
- **AppId 고정** (`{4F321868-99A6-487E-9B1C-9681A6AF63D9}`) - 버전이 바뀌어도 절대 재생성하지
  않는다. Windows가 "설치된 프로그램" 항목과 업그레이드 대상을 이 값으로 식별한다(§139).
- **AppMutex** - Desktop이 시작 시 만드는 전역 Mutex(`Global\CADWorkAssistant.SingleInstance`,
  `App.xaml.cs`)와 같은 이름을 설치 프로그램에도 등록해, 실행 중인 인스턴스가 있으면 Setup/
  Uninstall이 먼저 닫아달라고 안내한다(§146). 이 Mutex는 부수적으로 두 인스턴스가 같은 SQLite
  파일에 동시에 쓰는 것도 막아준다.
- **AutoCAD 실행 감지** - `[Code]` 섹션이 `tasklist`로 `acad.exe`를 확인하고, 실행 중이면
  계속 진행할지 사용자에게 묻는다. 강제 종료는 절대 하지 않는다(§147).
- **업그레이드 = 설치 파일 교체, 데이터는 그대로**. `[Files]`는 Desktop 실행 파일/DLL과 AutoCAD
  Bundle만 다루고, `%LOCALAPPDATA%\CADWorkAssistant\`(SQLite/로그/설정)는 건드리지 않는다.
- **Uninstall도 사용자 데이터를 삭제하지 않는다** (§135-137) - `[UninstallDelete]`는 실행
  파일/DLL과 AutoCAD Bundle만 제거 대상으로 명시한다. Bundle을 함께 지우는 이유는 Bundle
  자체에는 사용자 데이터가 없고(Manifest+DLL뿐), 남겨두면 다음 설치 전까지 AutoCAD가 존재하지
  않는 이전 DLL을 로드하려다 오류를 낼 수 있어서다.
- 데스크톱 바로가기는 선택 사항(기본 꺼짐, §141). AutoCAD를 함께 실행하지 않는다(§145).
- 화려한 installer skin을 쓰지 않는다 - Inno의 기본 native wizard 그대로(§144).
- **파일 인코딩**: `.iss` 파일은 순수 ASCII로 유지한다. Inno Setup 6의 스크립트 인코딩
  자동 감지는 UTF-8 BOM 유무에 의존하는데, 이 Repository의 파일 쓰기 도구가 BOM을 붙이지
  않아 한글이 섞이면 시스템 codepage로 잘못 해석되어 조용히 깨질 위험이 있다(실제로 겪음 -
  §8 참고). Setup의 표준 화면(언어 선택/Welcome/Ready/Finish)은 `compiler:Languages\Korean.isl`을
  통해 정상적으로 한국어로 뜬다 - 우리가 직접 쓰는 텍스트만 영문으로 유지한다.

## 6. Release Automation

`scripts/build-release.ps1` 한 커맨드:

```powershell
.\scripts\build-release.ps1
```

```text
Repository preflight -> Clean -> Restore -> Build (CADWorkAssistant.CI.slnf) -> Test (실패 시 즉시 중단) ->
Publish Desktop (self-contained win-x64) -> Build User Manual PDF -> Build AutoCAD Plugin -> Stage Bundle ->
Runtime Dependency Audit -> Build Installer (ISCC.exe) -> Installer Smoke Test -> SHA256 ->
Distribution folder -> release-manifest.json -> ZIP
```

옵션:

- `-SkipPlugin` - AutoCAD가 설치되지 않은 머신에서 Desktop만 릴리스.
- `-SkipInstaller` - Inno Setup 없이 publish 산출물까지만 확인.
- `-SkipTests` - 테스트를 이미 별도로 돌렸을 때만(기본은 항상 테스트를 돈다).

결과물:

```text
artifacts/release/CADWorkAssistant-0.9.0-RC/
  CADWorkAssistant-Setup-0.9.0-RC-x64.exe
  CADWorkAssistant-Setup-0.9.0-RC-x64.exe.sha256
  CAD_Work_Assistant_User_Guide_ko-KR.pdf
  RELEASE_NOTES_0.9.0-RC.md
  README_FIRST.txt
  THIRD_PARTY_NOTICES.txt
  release-manifest.json

artifacts/release/CADWorkAssistant-0.9.0-RC-x64.zip
artifacts/release/CADWorkAssistant-0.9.0-RC-x64.zip.sha256
```

`scripts/verify-distribution.ps1`이 최종 배포 폴더의 필수 파일, 버전, SHA256, 금지 파일을 확인한다.

## 7. Runtime Dependency Audit

`scripts/audit-runtime.ps1`이 `publish/desktop/`와 `installer/CADWorkAssistant.bundle/`을
검사해 다음이 없는지 확인한다. 하나라도 있으면 `build-release.ps1`이 즉시 중단된다.

- `CADWorkAssistant.FakeAutoCad.exe`, `*.Tests.dll` (개발/테스트 전용 산출물)
- `node.exe`, `python.exe`, `node_modules` (제품을 만드는 데만 쓰인 도구, 최종 제품에는 없음)
- `.claude`, `.21st` 폴더
- `acdbmgd.dll`/`acmgd.dll`/`accoremgd.dll` (Autodesk 호스트 DLL)
- 소스 파일(`*.cs`, `*.xaml`)

소스 레벨 감사(Claude/OpenAI/Anthropic/GPT/Ollama/MCP/HttpClient/WebClient 등 문자열 검색)는
`src/` 전체에 대해 별도로 1회 수행했고 전부 무해했다(자세한 내용은
`design-system/PRODUCTION_UI_REVIEW.md`의 LLM-Free Audit 섹션).

## 8. Installer Smoke Test

`scripts/test-release.ps1`이 실제 Setup exe로 다음을 검증한다.

```text
Installer exists -> Silent install (/VERYSILENT) -> Files exist ->
Desktop launches (Simulation Mode만 사용) -> Plugin bundle exists -> Process alive ->
Uninstall -> Binary removed -> User DB retained
```

실제 AutoCAD GUI는 이 스크립트에서도, 어떤 자동화 스크립트에서도 띄우지 않는다 - 이 개발 PC에서
AutoCAD 2024 GUI를 실제로 띄우면 그래픽 드라이버가 불안정해지는 게 확인되어 있다(`CLAUDE.md`).
실기 AutoCAD 연동 검증은 별도로 사람이 판단해서 진행한다.

## 9. Data & Settings Location

```text
Install folder (LocalAppData\Programs\CAD Work Assistant\)
  -> exe/dll만. 여기는 Uninstall 시 삭제된다.

%LOCALAPPDATA%\CADWorkAssistant\
  data\cadworkassistant.db              실사용 SQLite
  data\cadworkassistant.simulation.db   Simulation Mode 전용(실사용 데이터와 절대 안 섞임)
  logs\                                  Serilog 일 단위 롤링 로그

%APPDATA%\CADWorkAssistant\
  settings.json                          Milestone 0에서 이미 구현된 설정 파일

%APPDATA%\Autodesk\ApplicationPlugins\CADWorkAssistant.bundle\
  AutoCAD Plugin - Uninstall 시 함께 제거(사용자 데이터 아님)
```

Settings 화면(`Ctrl+,`)에서 "데이터 폴더 열기" 버튼으로 `%LOCALAPPDATA%\CADWorkAssistant\`를
탐색기로 바로 열 수 있다.

## 10. Not In Scope (§5, §161-166)

Telemetry/Analytics/Cloud Sync/Crash Upload/License Server/Update Server, 자동 업데이트(Setup
재실행이 곧 업데이트 방법), File Association, Context Menu, Auto-start(Windows 시작 프로그램
등록), Windows Service, Firewall Rule(Named Pipe만 쓰므로 필요 없음), Code Signing(인증서 없음
- 항목 11 참고).

## 11. Code Signing

인증서 없음. Unsigned installer로 진행했다(§157, 작업을 막지 않는다). Unsigned installer는
Windows SmartScreen이 "인식할 수 없는 앱"으로 경고할 수 있다(§158) - 정식 배포 전에는 코드
서명 인증서 구매를 검토해야 한다.
