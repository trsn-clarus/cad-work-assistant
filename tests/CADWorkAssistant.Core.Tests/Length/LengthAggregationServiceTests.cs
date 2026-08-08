using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Length;

namespace CADWorkAssistant.Core.Tests.Length;

public class LengthAggregationServiceTests
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-08-08T10:35:00+09:00");

    [Fact]
    public void Aggregate_ThreeObjects_MatchesKnownWorkOrderExample()
    {
        // Milestone 2 §7의 실제 값: 125331.214 + 81404.992 + 49204.454 = 255940.660 mm → 255.941 m
        var response = new LengthSelectionResponse(
            new[]
            {
                new CadLengthObjectDto("2A7F", SupportedGeometryType.Polyline, "A-WALL", 125331.214),
                new CadLengthObjectDto("2A80", SupportedGeometryType.Polyline, "A-WALL", 81404.992),
                new CadLengthObjectDto("2A81", SupportedGeometryType.Line, "A-WALL", 49204.454)
            },
            Array.Empty<string>(),
            DrawingUnit.Millimeters);

        var result = LengthAggregationService.Aggregate(response, "School_Roof.dwg", CreatedAt);

        Assert.Equal(255940.660, result.RawTotalLength, precision: 3);
        Assert.NotNull(result.DisplayValueMeters);
        Assert.Equal(255.940660, result.DisplayValueMeters!.Value, precision: 6);
        Assert.Equal("255.941 m", LengthFormatter.FormatMetersWithUnit(result.DisplayValueMeters.Value));
        Assert.Equal(3, result.ObjectCount);
    }

    [Fact]
    public void Aggregate_EmptySelection_ProducesZeroWithoutError()
    {
        var response = new LengthSelectionResponse(Array.Empty<CadLengthObjectDto>(), Array.Empty<string>(), DrawingUnit.Millimeters);

        var result = LengthAggregationService.Aggregate(response, "empty.dwg", CreatedAt);

        Assert.Equal(0, result.ObjectCount);
        Assert.Equal(0.0, result.RawTotalLength);
        Assert.Equal(0.0, result.DisplayValueMeters);
    }

    [Fact]
    public void Aggregate_NegativeLength_ThrowsBecauseGeometryShouldNeverProduceIt()
    {
        var response = new LengthSelectionResponse(
            new[] { new CadLengthObjectDto("9999", SupportedGeometryType.Line, "A-WALL", -10.0) },
            Array.Empty<string>(),
            DrawingUnit.Millimeters);

        Assert.Throws<ArgumentException>(() => LengthAggregationService.Aggregate(response, "bad.dwg", CreatedAt));
    }

    [Fact]
    public void Aggregate_UnitlessDrawing_DisplayValueIsNullNotZero()
    {
        var response = new LengthSelectionResponse(
            new[] { new CadLengthObjectDto("A001", SupportedGeometryType.Polyline, "A-WALL", 500.0) },
            Array.Empty<string>(),
            DrawingUnit.Unitless);

        var result = LengthAggregationService.Aggregate(response, "unitless.dwg", CreatedAt);

        Assert.Equal(500.0, result.RawTotalLength);
        Assert.Null(result.DisplayValueMeters);
    }

    [Fact]
    public void Aggregate_MixedSupportedAndUnsupported_ReportsExcludedCount()
    {
        var response = new LengthSelectionResponse(
            new[]
            {
                new CadLengthObjectDto("B001", SupportedGeometryType.Polyline, "A-WALL", 125331.214),
                new CadLengthObjectDto("B002", SupportedGeometryType.Line, "A-WALL", 49204.454)
            },
            new[] { "Hatch" },
            DrawingUnit.Millimeters);

        var result = LengthAggregationService.Aggregate(response, "mixed.dwg", CreatedAt);

        Assert.Equal(2, result.ObjectCount);
        Assert.Equal(1, result.ExcludedCount);
        Assert.Contains("Hatch", result.ExcludedObjectTypeNames);
    }

    [Fact]
    public void Aggregate_CentimeterDrawing_ConvertsTotalCorrectly()
    {
        var response = new LengthSelectionResponse(
            new[] { new CadLengthObjectDto("C001", SupportedGeometryType.Line, "A-WALL", 2500.0) },
            Array.Empty<string>(),
            DrawingUnit.Centimeters);

        var result = LengthAggregationService.Aggregate(response, "cm.dwg", CreatedAt);

        Assert.Equal(25.0, result.DisplayValueMeters);
    }
}
