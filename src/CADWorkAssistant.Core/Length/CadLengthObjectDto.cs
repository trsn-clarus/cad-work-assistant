namespace CADWorkAssistant.Core.Length;

/// <summary>
/// AutoCAD DBObject를 IPC로 그대로 넘기지 않기 위한 순수 DTO 하나 (§13). AutoCAD Plugin과
/// FakeAutoCAD 둘 다 이 타입으로 응답한다.
/// </summary>
public sealed class CadLengthObjectDto
{
    public CadLengthObjectDto(string handle, SupportedGeometryType geometryType, string layerName, double rawLength)
    {
        Handle = handle;
        GeometryType = geometryType;
        LayerName = layerName;
        RawLength = rawLength;
    }

    /// <summary>AutoCAD Handle 문자열 (예: "2A7F"). 향후 "CAD에서 원본 객체 찾기" 기능에 사용한다 (§29).</summary>
    public string Handle { get; }

    public SupportedGeometryType GeometryType { get; }

    public string LayerName { get; }

    /// <summary>Drawing Unit 기준 원본 값. 이미 m로 변환된 값이 아니다 (§20).</summary>
    public double RawLength { get; }
}
