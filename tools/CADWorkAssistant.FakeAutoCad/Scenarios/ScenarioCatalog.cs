using System;
using System.Collections.Generic;
using System.Linq;
using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Length;
using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.FakeAutoCad.Scenarios;

/// <summary>
/// Milestone 2 §8이 요구하는 최소 Scenario 집합 + 성능 테스트용 LargeSelection.
/// 새 Scenario는 여기에 한 항목만 추가하면 Integration Test와 수동 개발(Simulation Mode) 양쪽에서 쓸 수 있다.
/// </summary>
public static class ScenarioCatalog
{
    public const string DefaultScenarioName = "NormalSelection";

    private static readonly Dictionary<string, SimulationScenario> Scenarios = BuildAll()
        .ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Names => Scenarios.Keys;

    public static bool TryGet(string name, out SimulationScenario scenario) => Scenarios.TryGetValue(name, out scenario!);

    private static IEnumerable<SimulationScenario> BuildAll()
    {
        // §7 예시 그대로: Polyline 125331.214mm + Polyline 81404.992mm + Line 49204.454mm = 255940.660mm → 255.941 m
        yield return new SimulationScenario
        {
            Name = "NormalSelection",
            Objects = new[]
            {
                new CadLengthObjectDto("2A7F", SupportedGeometryType.Polyline, "A-WALL", 125331.214),
                new CadLengthObjectDto("2A80", SupportedGeometryType.Polyline, "A-WALL", 81404.992),
                new CadLengthObjectDto("2A81", SupportedGeometryType.Line, "A-WALL", 49204.454)
            }
        };

        yield return new SimulationScenario
        {
            Name = "SinglePolyline",
            Objects = new[] { new CadLengthObjectDto("2A7F", SupportedGeometryType.Polyline, "A-WALL", 125331.214) }
        };

        yield return new SimulationScenario
        {
            Name = "MultipleObjects",
            Objects = new[]
            {
                new CadLengthObjectDto("3001", SupportedGeometryType.Polyline, "A-WALL", 52310.500),
                new CadLengthObjectDto("3002", SupportedGeometryType.Polyline, "A-WALL", 18220.750),
                new CadLengthObjectDto("3003", SupportedGeometryType.Line, "A-WALL", 9040.125),
                new CadLengthObjectDto("3004", SupportedGeometryType.Line, "S-BEAM", 14500.000),
                new CadLengthObjectDto("3005", SupportedGeometryType.Arc, "A-WALL", 3210.980)
            }
        };

        yield return new SimulationScenario
        {
            Name = "EmptySelection",
            Objects = Array.Empty<CadLengthObjectDto>()
        };

        yield return new SimulationScenario
        {
            Name = "UnsupportedObject",
            Objects = Array.Empty<CadLengthObjectDto>(),
            ExcludedObjectTypeNames = new[] { "Hatch" }
        };

        yield return new SimulationScenario
        {
            Name = "MixedSupportedUnsupported",
            Objects = new[]
            {
                new CadLengthObjectDto("4001", SupportedGeometryType.Polyline, "A-WALL", 125331.214),
                new CadLengthObjectDto("4002", SupportedGeometryType.Line, "A-WALL", 49204.454)
            },
            ExcludedObjectTypeNames = new[] { "Hatch" }
        };

        yield return new SimulationScenario
        {
            Name = "UnitlessDrawing",
            Unit = DrawingUnit.Unitless,
            Objects = new[]
            {
                new CadLengthObjectDto("5001", SupportedGeometryType.Polyline, "A-WALL", 500.0),
                new CadLengthObjectDto("5002", SupportedGeometryType.Line, "A-WALL", 300.0)
            }
        };

        yield return new SimulationScenario
        {
            Name = "MetersDrawing",
            Unit = DrawingUnit.Meters,
            Objects = new[]
            {
                new CadLengthObjectDto("6001", SupportedGeometryType.Polyline, "A-WALL", 125.331),
                new CadLengthObjectDto("6002", SupportedGeometryType.Polyline, "A-WALL", 81.405),
                new CadLengthObjectDto("6003", SupportedGeometryType.Line, "A-WALL", 49.205)
            }
        };

        yield return new SimulationScenario { Name = "SelectionCancelled", LengthBehavior = SelectionBehavior.Cancelled };
        yield return new SimulationScenario { Name = "ConnectionLost", LengthBehavior = SelectionBehavior.DisconnectBeforeResponding };
        yield return new SimulationScenario { Name = "RequestTimeout", LengthBehavior = SelectionBehavior.HangForever };
        yield return new SimulationScenario { Name = "AutoCadError", LengthBehavior = SelectionBehavior.ReturnError };

        yield return new SimulationScenario
        {
            Name = "LargeSelection",
            Objects = Enumerable.Range(1, 1000)
                .Select(i => new CadLengthObjectDto(i.ToString("X4"), SupportedGeometryType.Polyline, "A-WALL", 1000 + i))
                .ToArray()
        };

        // --- Area (Milestone 3 §32-33) ---
        // §33 예시 그대로: Polyline 1,520,420,000 + 981,270,000 + 600,740,000 mm² = 3,102,430,000 mm² → 3,102.43 m²
        yield return new SimulationScenario
        {
            Name = "SingleClosedPolyline",
            AreaObjects = new[]
            {
                new CadAreaObjectDto("7001", SupportedAreaGeometryType.Polyline, "A-FLOOR", 1_520_420_000.0, isClosed: true)
            }
        };

        yield return new SimulationScenario
        {
            Name = "MultipleClosedPolylines",
            AreaObjects = new[]
            {
                new CadAreaObjectDto("7001", SupportedAreaGeometryType.Polyline, "A-FLOOR", 1_520_420_000.0, isClosed: true),
                new CadAreaObjectDto("7002", SupportedAreaGeometryType.Polyline, "A-FLOOR", 981_270_000.0, isClosed: true),
                new CadAreaObjectDto("7003", SupportedAreaGeometryType.Polyline, "A-FLOOR", 600_740_000.0, isClosed: true)
            }
        };

        yield return new SimulationScenario
        {
            Name = "OpenPolyline",
            AreaObjects = new[]
            {
                new CadAreaObjectDto("7010", SupportedAreaGeometryType.Polyline, "A-FLOOR", 0, isClosed: false)
            }
        };

        // §34 예시 그대로: 선택 4 / 닫힘 3 / 열림 1 → 3,102.43 m², 제외 1개
        yield return new SimulationScenario
        {
            Name = "MixedClosedOpen",
            AreaObjects = new[]
            {
                new CadAreaObjectDto("7020", SupportedAreaGeometryType.Polyline, "A-FLOOR", 1_520_420_000.0, isClosed: true),
                new CadAreaObjectDto("7021", SupportedAreaGeometryType.Polyline, "A-FLOOR", 981_270_000.0, isClosed: true),
                new CadAreaObjectDto("7022", SupportedAreaGeometryType.Polyline, "A-FLOOR", 600_740_000.0, isClosed: true),
                new CadAreaObjectDto("7023", SupportedAreaGeometryType.Polyline, "A-FLOOR", 0, isClosed: false)
            }
        };

        yield return new SimulationScenario { Name = "EmptyAreaSelection", AreaObjects = Array.Empty<CadAreaObjectDto>() };

        yield return new SimulationScenario { Name = "AreaSelectionCancelled", AreaBehavior = SelectionBehavior.Cancelled };

        yield return new SimulationScenario
        {
            Name = "UnsupportedAreaObject",
            AreaObjects = Array.Empty<CadAreaObjectDto>(),
            AreaExcludedObjectTypeNames = new[] { "Hatch" }
        };

        // 원래 §32 이름은 "MixedSupportedUnsupported"이지만 Length 시나리오와 이름이 겹쳐(같은
        // Dictionary Key) Area 접두어를 붙였다 - 딕셔너리 키 충돌을 피하기 위한 조정이다 (§7).
        yield return new SimulationScenario
        {
            Name = "AreaMixedSupportedUnsupported",
            AreaObjects = new[]
            {
                new CadAreaObjectDto("7030", SupportedAreaGeometryType.Polyline, "A-FLOOR", 1_520_420_000.0, isClosed: true)
            },
            AreaExcludedObjectTypeNames = new[] { "Hatch" }
        };

        // 닫혀 있지만 면적이 0에 가깝다 - Core.AreaAggregationService의 epsilon 판정을 검증한다 (§17, §19).
        yield return new SimulationScenario
        {
            Name = "ZeroArea",
            AreaObjects = new[]
            {
                new CadAreaObjectDto("7040", SupportedAreaGeometryType.Polyline, "A-FLOOR", 0.0, isClosed: true)
            }
        };

        // AutoCAD가 Area를 읽다가 예외를 던진 것을 흉내낸다 - NaN을 그대로 전달해 Core가
        // InvalidGeometry로 분류하는지 검증한다 (§17-18).
        yield return new SimulationScenario
        {
            Name = "InvalidArea",
            AreaObjects = new[]
            {
                new CadAreaObjectDto("7050", SupportedAreaGeometryType.Polyline, "A-FLOOR", double.NaN, isClosed: true)
            }
        };

        yield return new SimulationScenario
        {
            Name = "UnitlessAreaDrawing",
            Unit = DrawingUnit.Unitless,
            AreaObjects = new[]
            {
                new CadAreaObjectDto("7060", SupportedAreaGeometryType.Polyline, "A-FLOOR", 500_000.0, isClosed: true)
            }
        };

        yield return new SimulationScenario
        {
            Name = "MeterAreaDrawing",
            Unit = DrawingUnit.Meters,
            AreaObjects = new[]
            {
                new CadAreaObjectDto("7070", SupportedAreaGeometryType.Polyline, "A-FLOOR", 3102.43, isClosed: true)
            }
        };

        yield return new SimulationScenario { Name = "AreaConnectionLost", AreaBehavior = SelectionBehavior.DisconnectBeforeResponding };
        yield return new SimulationScenario { Name = "AreaRequestTimeout", AreaBehavior = SelectionBehavior.HangForever };
        yield return new SimulationScenario { Name = "AreaAutoCadError", AreaBehavior = SelectionBehavior.ReturnError };

        yield return new SimulationScenario
        {
            Name = "LargeAreaSelection",
            AreaObjects = Enumerable.Range(1, 1000)
                .Select(i => new CadAreaObjectDto(i.ToString("X4"), SupportedAreaGeometryType.Polyline, "A-FLOOR", 1000 + i, isClosed: true))
                .ToArray()
        };

        // --- Drawing Navigation / Selection / Isolation / Layer / Export (Milestone 5) ---
        // §134-135 예시 그대로: "실내마감표" 영역을 Crossing으로 선택하면 127개... 대신 헤드리스
        // 검증에 적당한 규모로 축소해서 타입/Layer 조합이 다양하게 섞이도록 구성했다.
        var normalLayers = new[]
        {
            new CadLayerDto("A-WALL", isOn: true, isFrozen: false, isLocked: false, isPlottable: true, colorIndex: 1, isCurrent: true),
            new CadLayerDto("A-FLOOR", isOn: true, isFrozen: false, isLocked: false, isPlottable: true, colorIndex: 3, isCurrent: false),
            new CadLayerDto("A-TEXT", isOn: true, isFrozen: false, isLocked: false, isPlottable: true, colorIndex: 7, isCurrent: false),
            new CadLayerDto("A-DOOR", isOn: false, isFrozen: false, isLocked: false, isPlottable: true, colorIndex: 2, isCurrent: false),
            new CadLayerDto("DEFPOINTS", isOn: true, isFrozen: false, isLocked: true, isPlottable: false, colorIndex: 7, isCurrent: false),
        };

        var normalDrawingObjects = new[]
        {
            new CadSelectedObjectDto("2A7F", "Polyline", "A-WALL", new CadBoundsDto(0, 0, 0, 12500, 8400, 0)),
            new CadSelectedObjectDto("2A80", "Polyline", "A-WALL", new CadBoundsDto(0, 0, 0, 15320, 12200, 0)),
            new CadSelectedObjectDto("2A81", "Line", "A-WALL", new CadBoundsDto(2000, 500, 0, 2000, 8000, 0)),
            new CadSelectedObjectDto("2B10", "MText", "A-TEXT", new CadBoundsDto(3000, 1000, 0, 4200, 1400, 0)),
            new CadSelectedObjectDto("2B11", "MText", "A-TEXT", new CadBoundsDto(3000, 2000, 0, 4200, 2400, 0)),
            new CadSelectedObjectDto("2C01", "BlockReference", "A-DOOR", new CadBoundsDto(5000, 0, 0, 5900, 100, 0)),
        };

        yield return new SimulationScenario
        {
            Name = "DrawingNavigationNormal",
            DrawingObjects = normalDrawingObjects,
            Layers = normalLayers
        };

        yield return new SimulationScenario { Name = "DrawingEmptySelection", DrawingObjects = Array.Empty<CadSelectedObjectDto>(), Layers = normalLayers };

        yield return new SimulationScenario { Name = "DrawingSelectionCancelled", DrawingSelectionBehavior = SelectionBehavior.Cancelled, Layers = normalLayers };
        yield return new SimulationScenario { Name = "DrawingConnectionLost", DrawingSelectionBehavior = SelectionBehavior.DisconnectBeforeResponding, Layers = normalLayers };
        yield return new SimulationScenario { Name = "DrawingRequestTimeout", DrawingSelectionBehavior = SelectionBehavior.HangForever, Layers = normalLayers };
        yield return new SimulationScenario { Name = "DrawingAutoCadError", DrawingSelectionBehavior = SelectionBehavior.ReturnError, Layers = normalLayers };

        yield return new SimulationScenario
        {
            Name = "LargeSelectionNavigation",
            Layers = normalLayers,
            DrawingObjects = Enumerable.Range(1, 1000)
                .Select(i => new CadSelectedObjectDto(
                    i.ToString("X4"),
                    i % 3 == 0 ? "Line" : "Polyline",
                    i % 2 == 0 ? "A-WALL" : "A-FLOOR",
                    new CadBoundsDto(i, i, 0, i + 100, i + 100, 0)))
                .ToArray()
        };

        yield return new SimulationScenario
        {
            Name = "LayerListNormal",
            DrawingObjects = normalDrawingObjects,
            Layers = Enumerable.Range(1, 60)
                .Select(i => new CadLayerDto($"LAYER-{i:D2}", isOn: i % 4 != 0, isFrozen: false, isLocked: false, isPlottable: true, colorIndex: (short)(i % 255), isCurrent: i == 1))
                .ToArray()
        };

        yield return new SimulationScenario
        {
            Name = "ExportSelectionSuccess",
            DrawingObjects = normalDrawingObjects,
            Layers = normalLayers
        };

        yield return new SimulationScenario
        {
            Name = "ExportSelectionError",
            DrawingObjects = normalDrawingObjects,
            Layers = normalLayers,
            ExportShouldFail = true
        };

        // --- AutoCAD Plot (Milestone 11 §95) ---
        var normalPlotDevices = new[]
        {
            new CadPlotDeviceDto("DWG To PDF.pc3", isPdfCapable: true),
            new CadPlotDeviceDto("HP LaserJet M404-M405", isPdfCapable: false)
        };

        var noPdfPlotDevices = new[]
        {
            new CadPlotDeviceDto("HP LaserJet M404-M405", isPdfCapable: false)
        };

        var fullPlotMedia = new[]
        {
            new CadPlotMediaDto("ISO_A4_(210.00_x_297.00_MM)", "ISO A4 (210.00 x 297.00 MM)", 210, 297),
            new CadPlotMediaDto("ISO_A3_(297.00_x_420.00_MM)", "ISO A3 (297.00 x 420.00 MM)", 297, 420)
        };

        var a4OnlyPlotMedia = new[]
        {
            new CadPlotMediaDto("ISO_A4_(210.00_x_297.00_MM)", "ISO A4 (210.00 x 297.00 MM)", 210, 297)
        };

        var fullCtbStyles = new[] { "acad.ctb", "monochrome.ctb", "Screening 50%.ctb" };
        var noMonochromeCtbStyles = new[] { "acad.ctb", "Screening 50%.ctb" };
        var stbStyles = new[] { "acad.stb", "Standard.stb" };

        var normalPlotLayouts = new[]
        {
            new CadPlotLayoutDto("Model", isModel: true, isCurrent: false),
            new CadPlotLayoutDto("Layout1", isModel: false, isCurrent: true),
            new CadPlotLayoutDto("Layout2", isModel: false, isCurrent: false)
        };

        yield return new SimulationScenario
        {
            Name = "PlotCapabilitiesNormal",
            PlotDevices = normalPlotDevices,
            PlotMedia = fullPlotMedia,
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotNamedStyleSheets = Array.Empty<string>(),
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts
        };

        yield return new SimulationScenario
        {
            Name = "PlotCapabilitiesCtb",
            PlotDevices = normalPlotDevices,
            PlotMedia = fullPlotMedia,
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotNamedStyleSheets = Array.Empty<string>(),
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts
        };

        yield return new SimulationScenario
        {
            Name = "PlotCapabilitiesStb",
            PlotDevices = normalPlotDevices,
            PlotMedia = fullPlotMedia,
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotNamedStyleSheets = stbStyles,
            PlotCurrentStyleMode = CadPlotStyleMode.Named,
            PlotLayouts = normalPlotLayouts
        };

        yield return new SimulationScenario
        {
            Name = "PlotCapabilitiesNoPdfDevice",
            PlotDevices = noPdfPlotDevices,
            PlotMedia = Array.Empty<CadPlotMediaDto>(),
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotNamedStyleSheets = Array.Empty<string>(),
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts
        };

        yield return new SimulationScenario
        {
            Name = "PlotCapabilitiesNoA3Media",
            PlotDevices = normalPlotDevices,
            PlotMedia = a4OnlyPlotMedia,
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotNamedStyleSheets = Array.Empty<string>(),
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts
        };

        yield return new SimulationScenario
        {
            Name = "PlotCapabilitiesNoMonochromeStyle",
            PlotDevices = normalPlotDevices,
            PlotMedia = fullPlotMedia,
            PlotColorDependentStyleSheets = noMonochromeCtbStyles,
            PlotNamedStyleSheets = Array.Empty<string>(),
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts
        };

        yield return new SimulationScenario
        {
            Name = "PlotWindowNormal",
            PlotDevices = normalPlotDevices,
            PlotMedia = fullPlotMedia,
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts,
            PlotWindowBehavior = SelectionBehavior.ReturnObjects,
            PlotWindow = new CadPlotWindowDto(0, 0, 18000, 12500)
        };

        yield return new SimulationScenario
        {
            Name = "PlotWindowCancelled",
            PlotDevices = normalPlotDevices,
            PlotMedia = fullPlotMedia,
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts,
            PlotWindowBehavior = SelectionBehavior.Cancelled
        };

        yield return new SimulationScenario
        {
            Name = "PlotSuccess",
            PlotDevices = normalPlotDevices,
            PlotMedia = fullPlotMedia,
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts,
            PlotWindow = new CadPlotWindowDto(0, 0, 18000, 12500),
            PlotDrawingPdfBehavior = PlotDrawingBehavior.Succeed
        };

        yield return new SimulationScenario
        {
            Name = "PlotBusy",
            PlotDevices = normalPlotDevices,
            PlotMedia = fullPlotMedia,
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts,
            PlotDrawingPdfBehavior = PlotDrawingBehavior.Busy
        };

        yield return new SimulationScenario
        {
            Name = "PlotFailure",
            PlotDevices = normalPlotDevices,
            PlotMedia = fullPlotMedia,
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts,
            PlotDrawingPdfBehavior = PlotDrawingBehavior.Failure
        };

        yield return new SimulationScenario
        {
            Name = "PlotDisconnect",
            PlotDevices = normalPlotDevices,
            PlotMedia = fullPlotMedia,
            PlotColorDependentStyleSheets = fullCtbStyles,
            PlotCurrentStyleMode = CadPlotStyleMode.ColorDependent,
            PlotLayouts = normalPlotLayouts,
            PlotDrawingPdfBehavior = PlotDrawingBehavior.DisconnectBeforeResponding
        };
    }
}
