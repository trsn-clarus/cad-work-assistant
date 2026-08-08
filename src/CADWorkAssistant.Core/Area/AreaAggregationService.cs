using System;
using System.Collections.Generic;
using System.Linq;

namespace CADWorkAssistant.Core.Area;

/// <summary>
/// 선택된 객체들을 분류(Valid/Open/Unsupported/InvalidGeometry)하고 유효한 것만 합산한다.
/// AutoCAD를 전혀 모르므로 AutoCAD 없이 테스트한다 (Length의 LengthAggregationService와 동일한 책임 분리, §5).
/// </summary>
public static class AreaAggregationService
{
    /// <summary>
    /// 부동소수점 오차 수준의 "사실상 0"을 실제 0과 구분하지 못해 생기는 오탐을 막는 최소 허용치.
    /// 도면 단위(제곱)가 무엇이든 이 정도로 작은 값은 실무적으로 의미 있는 면적일 수 없다 - 근거 없이
    /// 크게 잡지 않는다 (§19).
    /// </summary>
    public const double AreaEpsilon = 1e-6;

    public static AreaMeasurementResult Aggregate(
        AreaSelectionResponse response,
        string? drawingName,
        DateTimeOffset createdAt)
    {
        var items = new List<AreaMeasurementItem>();

        foreach (var obj in response.Objects)
        {
            items.Add(Classify(obj));
        }

        foreach (var typeName in response.ExcludedObjectTypeNames)
        {
            items.Add(new AreaMeasurementItem(handle: string.Empty, objectType: typeName, layerName: string.Empty, rawArea: 0, AreaObjectStatus.Unsupported));
        }

        var rawTotal = items.Where(i => i.Status == AreaObjectStatus.Valid).Sum(i => i.RawArea);

        double? displaySquareMeters = AreaUnitConverter.TryConvertToSquareMeters(rawTotal, response.Unit, out var squareMeters)
            ? squareMeters
            : null;

        return new AreaMeasurementResult(drawingName, items, rawTotal, response.Unit, displaySquareMeters, createdAt);
    }

    private static AreaMeasurementItem Classify(CadAreaObjectDto obj)
    {
        var objectType = obj.GeometryType.ToString();

        if (!obj.IsClosed)
        {
            return new AreaMeasurementItem(obj.Handle, objectType, obj.LayerName, rawArea: 0, AreaObjectStatus.Open);
        }

        if (double.IsNaN(obj.RawArea) || double.IsInfinity(obj.RawArea))
        {
            return new AreaMeasurementItem(obj.Handle, objectType, obj.LayerName, rawArea: 0, AreaObjectStatus.InvalidGeometry);
        }

        if (obj.RawArea < 0)
        {
            throw new ArgumentException(
                $"Object {obj.Handle} has a negative area ({obj.RawArea}) - AutoCAD geometry should never produce this.",
                nameof(obj));
        }

        if (obj.RawArea <= AreaEpsilon)
        {
            // 닫혀 있는데 면적이 0에 가깝다 - 퇴화된 형상으로 본다 (§17).
            return new AreaMeasurementItem(obj.Handle, objectType, obj.LayerName, rawArea: 0, AreaObjectStatus.InvalidGeometry);
        }

        return new AreaMeasurementItem(obj.Handle, objectType, obj.LayerName, obj.RawArea, AreaObjectStatus.Valid);
    }
}
