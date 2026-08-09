namespace CADWorkAssistant.Core.Text;

/// <summary>Milestone 12 §36-37 - 사용자가 AutoCAD에서 직접 지정한 삽입 위치(WCS). Desktop은 좌표를
/// 숫자로 입력받지 않는다 - 항상 AcquireTextInsertionPoint로 AutoCAD에서 실제 점을 받는다.</summary>
public sealed class CadPointDto
{
    public CadPointDto(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }
}
