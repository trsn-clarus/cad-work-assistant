# Release Checklist (Milestone 8)

새 버전을 릴리스할 때 순서대로 따른다. 아키텍처/설계 근거는 `docs/DEPLOYMENT.md`를 본다.

## 1. 버전 올리기

- [ ] `Directory.Build.props`의 `CwaVersion`을 새 버전으로 바꾼다 (다른 곳은 손대지 않는다 -
      Desktop/Plugin/Installer/Bundle Manifest 전부 이 값 하나를 따라간다).
- [ ] `CHANGELOG.md`가 있다면 갱신한다 (아직 없으면 이 릴리스에서 시작해도 된다).

## 2. 실행 중인 프로세스 정리

- [ ] `Get-Process -Name "CADWorkAssistant.Desktop","CADWorkAssistant.FakeAutoCad" | Stop-Process -Force`
      - 이전 Simulation Mode 세션이 남아 있으면 `publish`/빌드가 파일 잠김으로 실패한다.

## 3. 릴리스 빌드

```powershell
.\scripts\build-release.ps1
```

- [ ] 테스트 전체 통과 확인 (실패하면 스크립트가 자동으로 멈춘다 - Installer는 만들어지지
      않는다).
- [ ] `Runtime Dependency Audit` 통과 확인.
- [ ] `artifacts\installer\CADWorkAssistant-Setup-<version>-x64.exe`가 실제로 생겼는지, 크기가
      비정상적으로 작지 않은지(수십 MB 대) 확인.
- [ ] 같은 폴더의 `.sha256` 파일 확인.

AutoCAD가 없는 머신이라면 `-SkipPlugin`을 추가한다 - 이 경우 Desktop만 릴리스되고 Bundle은
이전 빌드 결과가 남아있지 않다는 점을 감안한다.

## 4. Installer 실제 검증

```powershell
.\scripts\test-release.ps1
```

- [ ] 9개 체크(installer exists / silent install / files exist / desktop launches / plugin
      bundle exists / process alive / uninstall / binary removed / user DB retained) 전부
      통과 확인.

## 5. 설치본 수동 UI 검수 (최소 1회, 특히 UI가 바뀐 릴리스라면 반드시)

`dotnet run`이 아니라 **실제 설치된 exe**로 확인한다 (`%LOCALAPPDATA%\Programs\CAD Work Assistant\`).

- [ ] Simulation Mode(`CWA_USE_FAKE_AUTOCAD=1` + `FakeAutoCad.exe --scenario NormalSelection`)로
      실행.
- [ ] Dashboard, Length, History, Drawing, Settings 최소 확인 - 폰트/아이콘/레이아웃이 개발
      빌드와 차이 없는지.
- [ ] 타이틀바/작업 표시줄 아이콘이 TRSN CLARUS 마크로 뜨는지.
- [ ] Settings 화면의 "데이터 위치"가 실제 `%LOCALAPPDATA%\CADWorkAssistant`를 가리키는지.

## 6. AutoCAD 연동 (AutoCAD가 있는 머신에서만, 사용자 확인 후)

- [ ] `%APPDATA%\Autodesk\ApplicationPlugins\CADWorkAssistant.bundle\PackageContents.xml` 존재
      확인.
- [ ] **실제 AutoCAD GUI를 띄우는 것은 이 개발 PC에서 하지 않는다** (그래픽 드라이버 불안정
      이력, `CLAUDE.md` §8). AutoCAD가 정상 동작하는 다른 머신에서: AutoCAD 실행 → 자동 로드
      확인(NETLOAD 없이) → Plugin 명령 정상 동작 확인 (`docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md`).

## 7. Git

- [ ] `git status` clean.
- [ ] 의미 단위로 나뉜 커밋들이 정확한지 확인 (`git log --oneline -20`).
- [ ] 태그가 필요하면 `git tag v<version>` (선택 사항 - 아직 이 프로젝트의 확립된 관례는 아님).

## 8. 배포

- [ ] `CADWorkAssistant-Setup-<version>-x64.exe`를 실제로 배포할 위치(사내 공유 폴더 등)에
      전달. 이 프로젝트는 아직 자동 업로드/배포 파이프라인이 없다 - 사람이 파일을 직접 옮긴다.
- [ ] SHA256 해시값을 배포 노트에 같이 남긴다.
- [ ] Unsigned installer이므로 최초 실행 시 Windows SmartScreen 경고가 뜰 수 있음을 받는 사람에게
      미리 안내한다("추가 정보 → 실행" 클릭 필요).

## 재설치/업그레이드 확인 (버전을 실제로 올리는 릴리스라면)

- [ ] 이전 버전이 설치된 PC에서 새 Setup을 실행 - 관리자 권한 요구 없이, 기존 설치 경로를
      기억해서(`UsePreviousAppDir=yes`) 덮어쓰는지 확인.
- [ ] 업그레이드 후 기존 Project/QuantityRecord/Verification/Review가 그대로 보이는지 확인
      (SQLite 파일은 손대지 않았으므로 마이그레이션이 필요하면 `CadWorkAssistantDatabase.
      OpenConnection()`이 앱 시작 시 알아서 처리한다 - `docs/PERSISTENCE.md` 참고).
