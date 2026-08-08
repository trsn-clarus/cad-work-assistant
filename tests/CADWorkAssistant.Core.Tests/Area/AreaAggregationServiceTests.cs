using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.Tests.Area;

public class AreaAggregationServiceTests
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-08-08T10:35:00+09:00");

    private static CadAreaObjectDto Closed(string handle, double rawArea, string layer = "A-FLOOR") =>
        new(handle, SupportedAreaGeometryType.Polyline, layer, rawArea, isClosed: true);

    private static CadAreaObjectDto Open(string handle, string layer = "A-FLOOR") =>
        new(handle, SupportedAreaGeometryType.Polyline, layer, rawArea: 0, isClosed: false);

    [Fact]
    public void Aggregate_ThreeClosedPolylines_MatchesKnownWorkOrderExample()
    {
        // §33 예시 그대로: 1,520,420,000 + 981,270,000 + 600,740,000 mm² = 3,102,430,000 mm² → 3,102.43 m²
        var response = new AreaSelectionResponse(
            new[]
            {
                Closed("7001", 1_520_420_000.0),
                Closed("7002", 981_270_000.0),
                Closed("7003", 600_740_000.0)
            },
            Array.Empty<string>(),
            DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "School_Roof.dwg", CreatedAt);

        Assert.Equal(3_102_430_000.0, result.RawTotalArea, precision: 3);
        Assert.NotNull(result.DisplayValueSquareMeters);
        Assert.Equal(3102.43, result.DisplayValueSquareMeters!.Value, precision: 6);
        Assert.Equal("3,102.43 m²", AreaFormatter.FormatSquareMetersWithUnit(result.DisplayValueSquareMeters.Value));
        Assert.Equal(3, result.SelectedCount);
        Assert.Equal(3, result.SupportedCount);
        Assert.Equal(0, result.ExcludedCount);
    }

    [Fact]
    public void Aggregate_SingleClosedPolyline_Valid()
    {
        var response = new AreaSelectionResponse(new[] { Closed("7001", 1_520_420_000.0) }, Array.Empty<string>(), DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "single.dwg", CreatedAt);

        Assert.Equal(1, result.SupportedCount);
        Assert.Equal(1_520_420_000.0, result.RawTotalArea);
    }

    [Fact]
    public void Aggregate_EmptySelection_ProducesZeroWithoutError()
    {
        var response = new AreaSelectionResponse(Array.Empty<CadAreaObjectDto>(), Array.Empty<string>(), DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "empty.dwg", CreatedAt);

        Assert.Equal(0, result.SelectedCount);
        Assert.Equal(0.0, result.RawTotalArea);
        Assert.Equal(0.0, result.DisplayValueSquareMeters);
    }

    [Fact]
    public void Aggregate_OpenPolylineOnly_ExcludedNotZeroArea()
    {
        // §16: 열린 Polyline을 0 m²로 계산하지 않는다 - Open으로 분류해 합산에서 제외한다.
        var response = new AreaSelectionResponse(new[] { Open("7010") }, Array.Empty<string>(), DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "open.dwg", CreatedAt);

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(0, result.SupportedCount);
        Assert.Equal(1, result.ExcludedCount);
        Assert.Single(result.OpenItems);
        Assert.Equal(AreaObjectStatus.Open, result.Items[0].Status);
    }

    [Fact]
    public void Aggregate_MixedClosedAndOpen_PartialSuccess()
    {
        // §34 예시: 선택 4 / 닫힘 3 / 열림 1 → 3,102.43 m², 제외 1개
        var response = new AreaSelectionResponse(
            new[]
            {
                Closed("7020", 1_520_420_000.0),
                Closed("7021", 981_270_000.0),
                Closed("7022", 600_740_000.0),
                Open("7023")
            },
            Array.Empty<string>(),
            DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "mixed.dwg", CreatedAt);

        Assert.Equal(4, result.SelectedCount);
        Assert.Equal(3, result.SupportedCount);
        Assert.Equal(1, result.ExcludedCount);
        Assert.Equal(3102.43, result.DisplayValueSquareMeters!.Value, precision: 6);
    }

    [Fact]
    public void Aggregate_UnsupportedOnly_NoValidObjects()
    {
        var response = new AreaSelectionResponse(Array.Empty<CadAreaObjectDto>(), new[] { "Hatch" }, DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "unsupported.dwg", CreatedAt);

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(0, result.SupportedCount);
        Assert.Single(result.UnsupportedItems);
        Assert.Equal("Hatch", result.UnsupportedItems[0].ObjectType);
    }

    [Fact]
    public void Aggregate_MixedValidAndUnsupported_ReportsBoth()
    {
        var response = new AreaSelectionResponse(new[] { Closed("7030", 1_520_420_000.0) }, new[] { "Hatch" }, DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "mixed2.dwg", CreatedAt);

        Assert.Equal(2, result.SelectedCount);
        Assert.Equal(1, result.SupportedCount);
        Assert.Equal(1, result.ExcludedCount);
    }

    [Fact]
    public void Aggregate_ClosedWithZeroArea_ClassifiedAsInvalidGeometryNotValid()
    {
        // §17: Closed==true, Area==0은 InvalidAreaGeometry로 처리한다 - Valid로 합산하지 않는다.
        var response = new AreaSelectionResponse(new[] { Closed("7040", 0.0) }, Array.Empty<string>(), DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "zero.dwg", CreatedAt);

        Assert.Equal(0, result.SupportedCount);
        Assert.Single(result.InvalidGeometryItems);
    }

    [Fact]
    public void Aggregate_ClosedWithBelowEpsilonArea_ClassifiedAsInvalidGeometry()
    {
        var response = new AreaSelectionResponse(new[] { Closed("7041", 1e-9) }, Array.Empty<string>(), DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "tiny.dwg", CreatedAt);

        Assert.Equal(0, result.SupportedCount);
        Assert.Single(result.InvalidGeometryItems);
    }

    [Fact]
    public void Aggregate_ClosedWithNaNArea_ClassifiedAsInvalidGeometry()
    {
        // AutoCAD가 Area를 읽다가 예외를 던졌다면 Handler가 NaN을 전달한다 (§17-18).
        var response = new AreaSelectionResponse(new[] { Closed("7050", double.NaN) }, Array.Empty<string>(), DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "invalid.dwg", CreatedAt);

        Assert.Equal(0, result.SupportedCount);
        Assert.Single(result.InvalidGeometryItems);
    }

    [Fact]
    public void Aggregate_ClosedWithInfiniteArea_ClassifiedAsInvalidGeometry()
    {
        var response = new AreaSelectionResponse(new[] { Closed("7051", double.PositiveInfinity) }, Array.Empty<string>(), DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "infinite.dwg", CreatedAt);

        Assert.Equal(0, result.SupportedCount);
        Assert.Single(result.InvalidGeometryItems);
    }

    [Fact]
    public void Aggregate_NegativeArea_ThrowsBecauseGeometryShouldNeverProduceIt()
    {
        var response = new AreaSelectionResponse(new[] { Closed("9999", -10.0) }, Array.Empty<string>(), DrawingUnit.Millimeters);

        Assert.Throws<ArgumentException>(() => AreaAggregationService.Aggregate(response, "bad.dwg", CreatedAt));
    }

    [Fact]
    public void Aggregate_UnitlessDrawing_DisplayValueIsNullNotZero()
    {
        var response = new AreaSelectionResponse(new[] { Closed("A001", 500_000.0) }, Array.Empty<string>(), DrawingUnit.Unitless);

        var result = AreaAggregationService.Aggregate(response, "unitless.dwg", CreatedAt);

        Assert.Equal(500_000.0, result.RawTotalArea);
        Assert.Null(result.DisplayValueSquareMeters);
    }

    [Fact]
    public void Aggregate_MeterDrawing_ValueIsUnchanged()
    {
        var response = new AreaSelectionResponse(new[] { Closed("M001", 3102.43) }, Array.Empty<string>(), DrawingUnit.Meters);

        var result = AreaAggregationService.Aggregate(response, "meters.dwg", CreatedAt);

        Assert.Equal(3102.43, result.DisplayValueSquareMeters!.Value, precision: 6);
    }

    [Fact]
    public void Aggregate_LargeObjectCount_SumsWithoutOverflowOrTimeout()
    {
        var objects = Enumerable.Range(1, 1000)
            .Select(i => Closed(i.ToString("X4"), 1000.0 + i))
            .ToArray();
        var response = new AreaSelectionResponse(objects, Array.Empty<string>(), DrawingUnit.Millimeters);

        var result = AreaAggregationService.Aggregate(response, "large.dwg", CreatedAt);

        Assert.Equal(1000, result.SupportedCount);
        Assert.Equal(objects.Sum(o => o.RawArea), result.RawTotalArea, precision: 3);
    }

    [Fact]
    public void Aggregate_FloatingPointAccumulation_MatchesDirectSum()
    {
        var objects = new[] { Closed("F1", 0.1), Closed("F2", 0.2), Closed("F3", 0.3) };
        var response = new AreaSelectionResponse(objects, Array.Empty<string>(), DrawingUnit.Meters);

        var result = AreaAggregationService.Aggregate(response, "float.dwg", CreatedAt);

        Assert.Equal(0.6, result.RawTotalArea, precision: 9);
    }
}
