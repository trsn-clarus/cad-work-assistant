using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Parapet;

namespace CADWorkAssistant.Core.Tests.Parapet;

public class ParapetCalculatorTests
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-08-08T10:35:00+09:00");

    private static ParapetInput Input(
        double lengthMeters = 32.118,
        double heightRaw = 1.0,
        DrawingUnit heightUnit = DrawingUnit.Meters,
        ParapetFaceMode faceMode = ParapetFaceMode.Single,
        bool topIncluded = false,
        double topWidthRaw = 0,
        DrawingUnit topWidthUnit = DrawingUnit.Meters) =>
        new(lengthMeters, heightRaw, heightUnit, faceMode, topIncluded, topWidthRaw, topWidthUnit);

    [Fact]
    public void Calculate_SingleFace_MatchesLxH()
    {
        var result = ParapetCalculator.Calculate(Input(faceMode: ParapetFaceMode.Single), CreatedAt);

        Assert.Equal(32.118, result.SideAreaSquareMeters, precision: 6);
        Assert.Equal(1, result.FaceMultiplier);
        Assert.Equal(0.0, result.TopAreaSquareMeters);
        Assert.Equal(32.118, result.TotalAreaSquareMeters, precision: 6);
    }

    [Fact]
    public void Calculate_BothFaces_DoublesTheSingleFaceArea()
    {
        // §27: 32.118 x 1.000 x 2 = 64.236
        var result = ParapetCalculator.Calculate(Input(faceMode: ParapetFaceMode.Both), CreatedAt);

        Assert.Equal(2, result.FaceMultiplier);
        Assert.Equal(64.236, result.SideAreaSquareMeters, precision: 6);
        Assert.Equal(64.236, result.TotalAreaSquareMeters, precision: 6);
    }

    [Fact]
    public void Calculate_TopOnly_ComputesLxWidthIndependentlyOfSide()
    {
        // §32: 32.118 x 0.150 = 4.818 (top uses the same L x W engine as side L x H)
        var result = ParapetCalculator.Calculate(
            Input(faceMode: ParapetFaceMode.Single, topIncluded: true, topWidthRaw: 0.150), CreatedAt);

        Assert.Equal(4.8177, result.TopAreaSquareMeters, precision: 4);
        Assert.True(result.TopIncluded);
    }

    [Fact]
    public void Calculate_SingleFacePlusTop_SumsBoth()
    {
        var result = ParapetCalculator.Calculate(
            Input(faceMode: ParapetFaceMode.Single, topIncluded: true, topWidthRaw: 0.150), CreatedAt);

        Assert.Equal(32.118 + 4.8177, result.TotalAreaSquareMeters, precision: 4);
    }

    [Fact]
    public void Calculate_BothFacesPlusTop_MatchesKnownWorkOrderExample()
    {
        // §34 실제 예시: Length 32.118 m, Height 1.000 m, Both, Top Width 0.150 m
        // Side 64.236 + Top 4.8177 = 69.0537 -> display "69.054 m²"
        var result = ParapetCalculator.Calculate(
            Input(faceMode: ParapetFaceMode.Both, topIncluded: true, topWidthRaw: 0.150), CreatedAt);

        Assert.Equal(64.236, result.SideAreaSquareMeters, precision: 6);
        Assert.Equal(4.8177, result.TopAreaSquareMeters, precision: 4);
        Assert.Equal(69.0537, result.TotalAreaSquareMeters, precision: 4);
        Assert.Equal("69.054 m²", AreaFormatter.FormatSquareMetersWithUnit(result.TotalAreaSquareMeters, decimalPlaces: 3));
        Assert.Equal("4.818 m²", AreaFormatter.FormatSquareMetersWithUnit(result.TopAreaSquareMeters, decimalPlaces: 3));
    }

    [Fact]
    public void Calculate_TopDisabled_IgnoresWidthEvenIfPositive()
    {
        // §38: 상부면 미포함이면 Width는 무시된다 - 값이 있어도 계산에 들어가지 않는다.
        var result = ParapetCalculator.Calculate(
            Input(topIncluded: false, topWidthRaw: 999.0), CreatedAt);

        Assert.False(result.TopIncluded);
        Assert.Equal(0.0, result.TopAreaSquareMeters);
        Assert.Equal(0.0, result.TopWidthMeters);
    }

    [Fact]
    public void Calculate_WidthInMillimeters_NormalizesToMeters()
    {
        var result = ParapetCalculator.Calculate(
            Input(topIncluded: true, topWidthRaw: 150.0, topWidthUnit: DrawingUnit.Millimeters), CreatedAt);

        Assert.Equal(0.15, result.TopWidthMeters, precision: 9);
    }

    [Fact]
    public void Calculate_WidthInCentimeters_NormalizesToMeters()
    {
        var result = ParapetCalculator.Calculate(
            Input(topIncluded: true, topWidthRaw: 15.0, topWidthUnit: DrawingUnit.Centimeters), CreatedAt);

        Assert.Equal(0.15, result.TopWidthMeters, precision: 9);
    }

    [Fact]
    public void Validate_ZeroHeight_ReportsHeightInvalid()
    {
        var validation = ParapetCalculator.Validate(0.0, topIncluded: false, topWidthRawValue: 0);

        Assert.False(validation.IsHeightValid);
        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Validate_NegativeHeight_ReportsHeightInvalid()
    {
        var validation = ParapetCalculator.Validate(-1.0, topIncluded: false, topWidthRawValue: 0);

        Assert.False(validation.IsHeightValid);
    }

    [Fact]
    public void Validate_TopIncludedWithZeroWidth_ReportsTopWidthInvalid()
    {
        var validation = ParapetCalculator.Validate(1.0, topIncluded: true, topWidthRawValue: 0.0);

        Assert.True(validation.IsHeightValid);
        Assert.False(validation.IsTopWidthValid);
        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Validate_TopIncludedWithNegativeWidth_ReportsTopWidthInvalid()
    {
        var validation = ParapetCalculator.Validate(1.0, topIncluded: true, topWidthRawValue: -0.1);

        Assert.False(validation.IsTopWidthValid);
    }

    [Fact]
    public void Validate_TopNotIncluded_IgnoresWidthValue()
    {
        var validation = ParapetCalculator.Validate(1.0, topIncluded: false, topWidthRawValue: -999.0);

        Assert.True(validation.IsTopWidthValid);
        Assert.True(validation.IsValid);
    }

    [Fact]
    public void Calculate_InvalidInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => ParapetCalculator.Calculate(Input(heightRaw: 0.0), CreatedAt));
    }

    [Fact]
    public void FaceMultiplier_Single_IsOne()
    {
        Assert.Equal(1, ParapetCalculator.FaceMultiplier(ParapetFaceMode.Single));
    }

    [Fact]
    public void FaceMultiplier_Both_IsTwo()
    {
        Assert.Equal(2, ParapetCalculator.FaceMultiplier(ParapetFaceMode.Both));
    }

    [Fact]
    public void Calculate_LargeLength_PreservesPrecision()
    {
        var result = ParapetCalculator.Calculate(
            Input(lengthMeters: 10_000.0, heightRaw: 2.5, faceMode: ParapetFaceMode.Both), CreatedAt);

        Assert.Equal(50_000.0, result.TotalAreaSquareMeters, precision: 3);
    }
}
