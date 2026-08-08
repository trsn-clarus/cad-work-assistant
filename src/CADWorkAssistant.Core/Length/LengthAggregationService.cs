using System;
using System.Linq;

namespace CADWorkAssistant.Core.Length;

/// <summary>
/// 여러 객체의 길이를 합산하고 단위를 변환한다. AutoCAD를 전혀 모르므로 AutoCAD 없이 테스트한다 (§25).
/// </summary>
public static class LengthAggregationService
{
    public static LengthMeasurementResult Aggregate(
        LengthSelectionResponse response,
        string? drawingName,
        DateTimeOffset createdAt)
    {
        foreach (var obj in response.Objects)
        {
            if (obj.RawLength < 0)
            {
                throw new ArgumentException(
                    $"Object {obj.Handle} has a negative length ({obj.RawLength}) - AutoCAD geometry should never produce this.",
                    nameof(response));
            }
        }

        var rawTotal = response.Objects.Sum(o => o.RawLength);

        double? displayMeters = LengthUnitConverter.TryConvertToMeters(rawTotal, response.Unit, out var meters)
            ? meters
            : null;

        return new LengthMeasurementResult(
            drawingName,
            response.Objects,
            response.ExcludedObjectTypeNames,
            rawTotal,
            response.Unit,
            displayMeters,
            createdAt);
    }
}
