namespace CADWorkAssistant.Core.Drawing;

/// <summary>
/// AutoCAD의 Extents3d를 IPC로 그대로 노출하지 않기 위한 순수 DTO (Milestone 5 §15). Drawing Unit
/// 좌표를 그대로 담는다 - Zoom은 화면 좌표 공간을 다루는 것이지 길이/면적처럼 m 단위로 환산해서
/// 보여줄 값이 아니라서 단위 변환이 필요 없다.
/// </summary>
public sealed class CadBoundsDto
{
    public CadBoundsDto(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
    }

    public double MinX { get; }
    public double MinY { get; }
    public double MinZ { get; }
    public double MaxX { get; }
    public double MaxY { get; }
    public double MaxZ { get; }
}
