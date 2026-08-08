using System.Collections.Generic;

namespace CADWorkAssistant.Core.Drawing;

/// <summary>
/// 여러 객체의 Bounds를 하나로 합친다(union) - "선택 영역 보기(Zoom Selection)"가 선택된 모든 객체를
/// 한 화면에 담을 수 있는 하나의 Bounds가 필요하기 때문이다 (Milestone 5 §66-67). AutoCAD API 비의존,
/// 유닛 테스트 가능.
/// </summary>
public static class BoundsAggregator
{
    /// <summary>비어 있거나 전부 null이면 null - "Bounds가 없다"를 명확히 구분한다.</summary>
    public static CadBoundsDto? Aggregate(IEnumerable<CadBoundsDto?> bounds)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        var any = false;

        foreach (var b in bounds)
        {
            if (b is null || !IsFinite(b))
            {
                continue;
            }

            any = true;
            if (b.MinX < minX) minX = b.MinX;
            if (b.MinY < minY) minY = b.MinY;
            if (b.MinZ < minZ) minZ = b.MinZ;
            if (b.MaxX > maxX) maxX = b.MaxX;
            if (b.MaxY > maxY) maxY = b.MaxY;
            if (b.MaxZ > maxZ) maxZ = b.MaxZ;
        }

        return any ? new CadBoundsDto(minX, minY, minZ, maxX, maxY, maxZ) : null;
    }

    /// <summary>NaN/Infinity가 섞인 잘못된 Extents(예: 빈 Block의 GeometricExtents)를 걸러낸다.</summary>
    private static bool IsFinite(CadBoundsDto b) =>
        IsFiniteValue(b.MinX) && IsFiniteValue(b.MinY) && IsFiniteValue(b.MinZ) &&
        IsFiniteValue(b.MaxX) && IsFiniteValue(b.MaxY) && IsFiniteValue(b.MaxZ);

    private static bool IsFiniteValue(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
