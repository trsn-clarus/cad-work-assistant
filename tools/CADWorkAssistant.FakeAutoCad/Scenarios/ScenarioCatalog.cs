using System;
using System.Collections.Generic;
using System.Linq;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Length;

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

        yield return new SimulationScenario { Name = "SelectionCancelled", Behavior = SelectionBehavior.Cancelled };
        yield return new SimulationScenario { Name = "ConnectionLost", Behavior = SelectionBehavior.DisconnectBeforeResponding };
        yield return new SimulationScenario { Name = "RequestTimeout", Behavior = SelectionBehavior.HangForever };
        yield return new SimulationScenario { Name = "AutoCadError", Behavior = SelectionBehavior.ReturnError };

        yield return new SimulationScenario
        {
            Name = "LargeSelection",
            Objects = Enumerable.Range(1, 1000)
                .Select(i => new CadLengthObjectDto(i.ToString("X4"), SupportedGeometryType.Polyline, "A-WALL", 1000 + i))
                .ToArray()
        };
    }
}
