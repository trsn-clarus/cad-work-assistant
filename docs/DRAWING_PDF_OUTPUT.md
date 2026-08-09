# AutoCAD Plot + Drawing PDF Output (Milestone 11)

## 1. 목표와 Milestone 10 PDF와의 차이

Milestone 10의 PDF(`docs/PDF_EXPORT.md`)는 저장된 수량 데이터를 렌더링한 **문서**다. 이 Milestone의
PDF는 완전히 다른 것이다 - **실제 AutoCAD Plot 엔진**(`Autodesk.AutoCAD.PlottingServices`)으로 현재
Layout 또는 사용자가 지정한 Model Space 영역을 실제 도면 그대로 PDF로 출력한다. 두 기능은 서로 다른
서브시스템이고, 코드도 전혀 공유하지 않는다 - `Documents.Pdf.QuantityPdfBuilder`(PDFsharp/MigraDoc)와
`AutoCAD.Ipc.Handlers.PlotDrawingPdfHandler`(AutoCAD Managed API)는 서로를 모른다. OUTPUT 메뉴에서도
"PDF"(수량 보고서)와 "Plot"(도면 PDF 출력)을 별도 항목으로 분리했다.

## 2. Milestone 11A / 11B 분리

- **11A(이번 범위, 실제 AutoCAD 없이 구현/검증 완료)**: Plot 도메인 모델, IPC 계약, Capability 조회
  아키텍처, Preset, 용지 매칭, CTB/STB 호환 로직, 출력 파일명, Desktop Workflow/UI, FakeAutoCad
  Scenario, Headless IPC E2E, Persistence 이력, Release 패키징.
- **11B(BLOCKED - 실제 AutoCAD 2024 하드웨어 필요)**: 실제 장치/용지 목록 조회 정확성, 실제 PDF
  생성 정확성, 실제 A3/A4 물리 출력 크기, 실제 CTB/STB 시각 결과, 실제 monochrome.ctb 시각 결과,
  회전된 UCS/Pan/Zoom 상태에서 Window Plot 좌표 정확성, 실제 Layout PDF 정확성, PlotEngine 런타임
  동작, 진행률/취소 동작, DWG 수정 플래그/저장 프롬프트 영향, 생성된 PDF의 실제 시각적 정확성.

이 PC는 AutoCAD 2024 GUI를 실행하면 그래픽 드라이버가 불안정해지는 문제가 있어(Milestone 1, §8
`AUTOCAD_REAL_MACHINE_CHECKLIST.md`) 11B는 이 PC에서 수행할 수 없다. Milestone 8.5(Plugin NETLOAD
실사용 검증)와 마찬가지로 명확히 BLOCKED로 남긴다.

## 3. 아키텍처

```text
Desktop.ViewModels.DrawingPdfExportViewModel
  │ (탭 활성화) LoadCapabilitiesAsync
  ▼
Desktop.Services.PlotCapabilityCoordinator (IPlotCapabilityCoordinator)
  │ GetPlotCapabilities IPC 호출
  ▼
AutoCAD.Ipc.Handlers.GetPlotCapabilitiesHandler ─▶ PlotCapabilityReader(장치/용지/스타일/Layout 실측 조회)
  │
  │ (Window Scope 선택 시) "영역 지정" 클릭
  ▼
Desktop.Services.PlotWindowSelector (static helper, LengthSelectionCoordinator와 같은 패턴)
  │ AcquirePlotWindow IPC 호출 (인터랙티브)
  ▼
AutoCAD.Ipc.Handlers.AcquirePlotWindowHandler ─▶ Editor.GetPoint/GetCorner로 두 점 획득
  │
  │ (SaveFileDialog에서 경로 확정 후) "PDF로 저장" 클릭
  ▼
Desktop.Services.DrawingPdfExportCoordinator (IDrawingPdfExportCoordinator)
  │ PlotDrawingPdf IPC 호출 + 성공 시 AddDrawingPdfExportRecordAsync
  ▼
AutoCAD.Ipc.Handlers.PlotDrawingPdfHandler
  │ 임시 override PlotSettings 구성 → PlotSettingsValidator로 장치/용지/스타일/영역 적용
  │ → PlotInfoValidator.Validate → PlotFactory.CreatePublishEngine()
  │ → BeginPlot/BeginDocument/BeginPage/BeginGenerateGraphics/.../EndPlot
  ▼
.pdf (임시 파일 → 원자적 File.Move) + ExportRecord(ExportType=DrawingPdf)+ActivityRecord
```

전체 시스템 안에서의 위치는 [`ARCHITECTURE.md`](./ARCHITECTURE.md) §8.10, AutoCAD 연동 세부는
[`AUTOCAD_INTEGRATION.md`](./AUTOCAD_INTEGRATION.md) §11 참고.

## 4. Core 도메인 모델 (`CADWorkAssistant.Core.Plot`, AutoCAD 비의존)

| 타입 | 역할 |
|---|---|
| `CadPlotScope` | `CurrentLayout` \| `Window`. Extents는 이번 범위에서 뺐다(§9, 필요할 때 구현) |
| `CadPlotOrientation` | `Auto` \| `Portrait` \| `Landscape`. `PlotOrientationResolver`가 Window 종횡비로 Auto를 해석 |
| `CadPlotColorMode` | `KeepExisting` \| `Monochrome`(의도) |
| `CadPlotStyleMode` | `ColorDependent`(CTB) \| `Named`(STB) - `Database.PlotStyleMode`를 도메인 값으로 옮김 |
| `CadPaperSize`/`CadPaperSizeCatalog` | A4/A3만 내장(첫 버전) - Portrait 기준 mm 물리 치수 |
| `CadPlotWindowDto` | 사용자가 지정한 2D 영역(MinX/MinY/MaxX/MaxY). Milestone 5의 `CadBoundsDto`(선택 객체 bounding box, 3D)와 의도적으로 분리 - 개념이 다르면 같은 DTO를 재사용하지 않는다 |
| `CadPlotDeviceDto`/`CadPlotMediaDto`/`CadPlotLayoutDto` | 실제 AutoCAD가 보고한 장치/용지/Layout 원본 데이터 |
| `PlotPaperMatcher` | 장치의 실측 mm 치수로 A4/A3를 매칭(`ToleranceMm = 2.0`, 이름 있는 상수) - 이름 문자열(`"ISO_A3"` 등)로 추측하지 않는다 |
| `PlotOrientationResolver` | Auto 방향을 Window 종횡비로 결정, 참고할 게 없으면 Portrait |
| `PlotStyleResolver`/`PlotStyleResolution` | "흑백" 의도를 실제 `monochrome.ctb` 존재 여부로 해석. STB 도면에는 절대 CTB를 강제하지 않는다 |
| `PlotPdfDeviceSelector` | PDF 가능 장치 중 `"DWG To PDF.pc3"` 우선, 없으면 첫 PDF 가능 장치, 없으면 null |
| `PlotOutputFileNameService` | 제안 파일명 생성 - Milestone 5의 `ExportFileNameService.Sanitize` 재사용 |

## 5. IPC 계약 (`Core.Ipc.IpcMessageTypes`)

| 메시지 | 방향 | 비고 |
|---|---|---|
| `GetPlotCapabilities` | Read-only, `InvokeAsync` | 장치/용지(PDF 장치 기준)/CTB·STB 목록/현재 도면 스타일 모드/Layout 목록 |
| `AcquirePlotWindow` | 인터랙티브, `InvokeInCommandContextAsync` | `Editor.GetPoint`+`GetCorner`로 두 점 획득 - `SelectDrawingObjectsHandler`와 같은 UX 패턴, Selection은 하지 않는다 |
| `PlotDrawingPdf` | Non-interactive, `InvokeAsync` | Scope/LayoutName/Window/PaperSizeName/Orientation/ColorMode/TargetFilePath를 받아 실제 Plot 실행 |

세 메시지 모두 새 Protocol Version이 필요 없다(문자열 상수만 추가, 기존 IPC 봉투 그대로).

## 6. AutoCAD Plugin 구현 — 실제 API, 리플렉션으로 전량 검증

모든 타입/메서드/열거값은 이 PC에 실제 설치된 AutoCAD 2024(`acdbmgd.dll`/`acmgd.dll`/
`accoremgd.dll`)를 PowerShell 리플렉션으로 직접 확인한 뒤 사용했다 - 온라인 예제 코드를 추측으로
옮기지 않았다. 실제로 발견/정정한 것들:

- `PlotFactory`에는 `CreatePublishEngine()`과 `CreatePreviewEngine(int)`만 있다 - **`CreatePlotEngine()`은
  존재하지 않는다**(일부 오래된 온라인 예제가 잘못 가정하는 부분).
- `PlotType`이라는 이름의 **서로 다른 두 enum**이 존재한다 -
  `Autodesk.AutoCAD.DatabaseServices.PlotType`(Display/Extents/Limits/View/Window/Layout)과
  `Autodesk.AutoCAD.PlottingServices.PlotType`. `PlotSettingsValidator.SetPlotType`은
  `DatabaseServices.PlotType`을 받는다 - 두 네임스페이스를 동시에 `using`하면 컴파일 에러(CS0104)가
  나서 완전한 이름으로 구분해야 한다.
- `Extents2d`는 `Autodesk.AutoCAD.Geometry`가 아니라 **`Autodesk.AutoCAD.DatabaseServices`**
  네임스페이스에 있다. 4-double 생성자(`Extents2d(minX, minY, maxX, maxY)`)가 있어 `Point2d`를
  따로 만들 필요가 없다.
- `PlotConfig`/`PlotConfigManager`/`MediaBounds`/`PlotConfigInfo`는 전부
  `Autodesk.AutoCAD.PlottingServices` 네임스페이스에 있다(`DatabaseServices`가 아니다).
- `PlotSettings`/`PlotInfo`/`PlotEngine`은 모두 `IDisposable` - `using`으로 감싼다.
- `PlotSettingsValidator.RefreshLists(PlotSettings)`가 실제로 존재한다 - `SetPlotConfigurationName`
  이후, `SetCanonicalMediaName` 이전에 호출하는 것이 공식 샘플의 관례다.

자세한 시그니처는 `src/CADWorkAssistant.AutoCAD/Ipc/PlotCapabilityReader.cs`/
`Ipc/Handlers/PlotDrawingPdfHandler.cs`의 XML 주석에 §번호와 함께 남겨두었다.

### 6.1 GetPlotCapabilitiesHandler

`PlotConfigManager.Devices`를 순회하며 각 장치를 `PlotConfigManager.SetCurrentConfig(name)`으로
잠깐 로드해 `IsPlotToFile`+`DefaultFileExtension=="pdf"`를 확인한다 - 이 API가 AutoCAD의 "현재
장치"를 전역으로 바꾸는 부작용이 있어, 조회가 끝나면 원래 장치로 되돌린다(원복이 Plot 대화상자
기본값에 실제로 영향을 주지 않는지는 11B 검증 대상). PDF 장치 선택 정책은 `PlotPdfDeviceSelector`
한 곳에만 있다 - 여기서 다시 구현하지 않는다.

### 6.2 AcquirePlotWindowHandler

`SelectDrawingObjectsHandler`와 완전히 같은 `GetPoint`→`GetCorner` UX를 재사용한다. 다만 결과가
선택된 객체가 아니라 두 점의 좌표 자체이므로 `SelectWindow`/`SelectCrossingWindow`는 호출하지 않는다.
WCS 좌표를 그대로 담는다 - 회전된 UCS/Pan/Zoom 상태에서의 정확성은 11B 검증 대상이다.

### 6.3 PlotDrawingPdfHandler

1. `PlotFactory.ProcessPlotState != NotPlotting`이면 즉시 실패(다른 Plot이 진행 중).
2. Transaction 안에서 대상 Layout을 찾고, **Transaction이 열려 있는 동안** `new PlotSettings(layout.ModelType)` +
   `CopyFrom(layout)`으로 임시 override 설정을 만든다 - Layout(DBObject)은 Transaction이 닫히면
   더 이상 안전하게 참조할 수 없어, CopyFrom까지 끝낸 뒤에만 Transaction을 커밋한다(PlotSettings
   자체는 별도 detached 객체라 이후에도 안전하게 쓸 수 있다).
3. `PlotSettingsValidator`로 장치/용지/스타일/방향/Plot Type/Window 영역을 적용한다 - **원본
   Layout의 PlotSettings는 전혀 건드리지 않는다**(CLAUDE.md 절대 원칙 1).
4. `PlotInfoValidator().Validate(plotInfo)` → `PlotFactory.CreatePublishEngine()`으로 전체
   Begin/End 시퀀스를 실행한다.
5. 임시 파일(`.plot_{guid}.tmp`)로 먼저 쓰고, 성공 시에만 대상 경로로 교체한다(net48이라
   `File.Move`에 overwrite 오버로드가 없어 `File.Delete` 후 `File.Move` 두 단계로 처리) -
   Milestone 9/10의 원자적 저장과 같은 원칙.

## 7. FakeAutoCad — 실제 Plot을 절대 흉내내지 않는다

`FakePlotDrawingPdfHandler`는 Milestone 10의 `QuantityPdfBuilder`를 재사용하지 않는다(명시적으로
금지됨 - 완전히 다른 서브시스템이다). `FakeExportSelectionHandler`가 가짜 DWG 대신 평문 안내
텍스트를 쓰는 것과 같은 방식으로, 대상 경로에 `"CADWorkAssistant FakeAutoCad placeholder plot -
not a real AutoCAD Plot output."`만 남긴다 - 진짜 PDF 바이너리를 흉내내려 하지 않는다. 응답의
`Warning` 필드에도 같은 취지의 문구를 담아 Desktop UI가 그대로 사용자에게 보여준다("이것은
FakeAutoCad Simulation 결과입니다").

`ScenarioCatalog`에 12개 Plot Scenario를 추가했다:

| Scenario | 검증하는 것 |
|---|---|
| `PlotCapabilitiesNormal`/`PlotCapabilitiesCtb` | CTB 도면, PDF 장치/A4·A3 용지/monochrome.ctb 모두 사용 가능 |
| `PlotCapabilitiesStb` | STB 도면 - Monochrome 사전 설정이 항상 Unavailable로 판정되는지 |
| `PlotCapabilitiesNoPdfDevice` | PDF 가능 장치가 하나도 없을 때 |
| `PlotCapabilitiesNoA3Media` | A4만 지원하는 장치일 때 |
| `PlotCapabilitiesNoMonochromeStyle` | CTB 도면인데 monochrome.ctb가 목록에 없을 때 |
| `PlotWindowNormal`/`PlotWindowCancelled` | AcquirePlotWindow 성공/Esc 취소 |
| `PlotSuccess`/`PlotBusy`/`PlotFailure`/`PlotDisconnect` | PlotDrawingPdf의 성공/Busy/실패/연결 끊김 |

`SelectionBehavior`(기존, Length/Area/Drawing이 공유)를 `PlotWindowBehavior`에도 그대로
재사용했다 - AcquirePlotWindow도 결국 인터랙티브 Selection의 한 형태이기 때문이다. `PlotDrawingPdf`는
Busy라는 새 실패 종류가 있어 전용 `PlotDrawingBehavior` enum을 별도로 뒀다.

## 8. Desktop UI — OUTPUT > Plot

`DrawingPdfExportViewModel`은 상태를 다음과 같이 노출한다: `IsConnected`(Disconnected 안내) →
`IsLoadingCapabilities` → `ShowLoadErrorMessage`/`ShowNoPdfDeviceMessage`/`ShowReadyForm`. Ready
화면은:

- **출력 범위**: 현재 Layout(드롭다운으로 Layout 선택) 또는 Model 영역 지정(버튼으로 AcquirePlotWindow
  실행, 결과를 "18,000 × 12,500 (도면 단위)" 형태로 요약 표시)
- **용지**: A4/A3 - `PlotPaperMatcher.FindMatch`로 실제 지원 여부를 계산해 지원하지 않는 용지는
  라디오 자체를 비활성화한다(하드코딩된 가정 없음)
- **방향**: 자동/세로/가로
- **색상**: 컬러(기존 설정 유지)/흑백 - `PlotStyleResolver`로 계산한 `IsMonochromeAvailable`이
  false면 비활성화

저장 시 `PlotOutputFileNameService.SuggestFileName`으로 제안 파일명을 만들어 native
`SaveFileDialog`를 띄운다(Milestone 5/9/10과 같은 관례 - 커스텀 파일 브라우저를 만들지 않는다).
성공하면 파일 열기/폴더 열기 버튼과 함께 완료 메시지(+FakeAutoCad Simulation일 때는 경고 문구도)를
보여주고, `IProjectContextService.AddDrawingPdfExportRecordAsync`로 이력을 남긴다.

## 9. Persistence — Export 이력

`ExportRecord.ExportType`에 `DrawingPdf` 상수를 추가했다(Migration 불필요 - TEXT 컬럼). Milestone
10의 `PdfQuantityReport`와 값을 공유하지 않는다 - 완전히 다른 서브시스템의 산출물이기 때문이다.
`AddDrawingPdfExportRecordAsync`가 `AddPdfExportRecordAsync`와 같은 트랜잭션 패턴(ExportRecord +
ActivityRecord "도면 PDF 출력 완료")으로 저장한다.

## 10. 실제로 검증한 것 (Simulation Mode, 2026-08-09)

FakeAutoCad.exe + Desktop.exe를 실제로 별도 프로세스 두 개로 띄워서 확인함(스크린샷 기반):

1. `PlotSuccess` Scenario: Plot 탭 진입 → Capability 자동 로드 → 용지/방향/색상 Preset이 실제
   Capability를 반영해 렌더링됨(A4/A3 모두 활성, 흑백 활성) 확인
2. "영역 지정" 클릭 → 실제 Named Pipe 왕복으로 AcquirePlotWindow 호출 → "18,000 × 12,500 (도면
   단위)" 정확히 표시, "PDF로 저장" 버튼이 그제서야 활성화됨(Window Scope는 영역이 있어야 저장
   가능) 확인
3. "PDF로 저장" → 실제 native SaveFileDialog가 제안 파일명
   `School_Roof_영역출력_A4_컬러_20260809.pdf`로 뜸(`PlotOutputFileNameService` 규칙과 정확히 일치)
   → 저장 → 실제 파일이 디스크에 생성됨을 직접 확인(`Get-ChildItem`), 내용이 FakeAutoCad
   placeholder 문구인 것도 확인 → UI가 "PDF 저장 완료" + 경고 문구 + 파일 열기/폴더 열기 버튼을
   표시, Property Inspector의 "최근 저장"도 갱신됨
4. `PlotCapabilitiesNoPdfDevice` Scenario로 재연결 → "PDF로 출력 가능한 Plot 장치를 찾지
   못했습니다." 안내와 함께 Ready 폼 자체가 숨겨짐, Property Inspector "PDF 장치: 없음" 확인
5. "현재 Layout" Scope로 전환 → "영역 지정" 버튼이 비활성화되고 Property Inspector가 "현재
   Layout"으로 갱신됨 확인
6. `PlotFailure` Scenario로 CurrentLayout Scope 저장 시도 → "도면 PDF 저장에 실패했습니다.\n\n
   AutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요." 붉은 오류 메시지 표시, 원시 예외/스택트레이스
   노출 없음 확인(CLAUDE.md 절대 원칙 4)
7. FakeAutoCad 프로세스를 종료 → "AutoCAD에 연결되어 있지 않습니다." 안내와 사이드바 "AutoCAD Not
   Running" 확인
8. Release 빌드로 만든 실제 설치 프로그램(`CADWorkAssistant-Setup-0.8.0-x64.exe`)을 `/VERYSILENT`로
   설치 → 설치된 `CADWorkAssistant.Desktop.exe`를 Simulation Mode로 실행 → Plot 탭이 정상 도달/
   동작함을 확인 → 검증 후 조용히 제거(uninstaller `/VERYSILENT`)

## 11. 이번 범위에서 의도적으로 하지 않은 것

- **Extents Plot Scope** - Window/CurrentLayout으로 첫 버전 충분(§9)
- **A2/A1/A0 용지, Custom 용지 크기** - A4/A3만 첫 버전(§23)
- **여러 Layout 동시 출력(Batch Plot)** - 한 번에 한 출력만
- **진행률 표시/취소** - FakeAutoCad 흉내가 항상 즉시 끝나 첫 버전에서 UI 진행률 바 없이 "출력
  중..." 텍스트만 표시. 실제 AutoCAD Plot의 소요 시간/취소 필요성은 11B에서 재평가
- **Plot Preview(대화상자 내 미리보기)** - 저장 후 OS 뷰어로 확인하는 흐름으로 충분하다고 판단
- **PDF 파일에 실제로 몇 페이지가 담기는지에 대한 강한 보장** - 항상 단일 페이지 출력을 전제로
  구현했다(`pageCount: 1`로 이력에 기록) - 여러 페이지 Plot은 이번 범위 밖
