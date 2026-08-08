namespace CADWorkAssistant.Core.Area;

/// <summary>
/// AutoCAD DBObject를 IPC로 그대로 넘기지 않기 위한 순수 DTO (Length의 CadLengthObjectDto와 동일한 이유,
/// §13). AutoCAD Plugin과 FakeAutoCAD 둘 다 이 타입으로 응답한다. Length와 달리 "닫혀 있는가"가
/// 값 자체의 유효성을 좌우하므로 <see cref="IsClosed"/>를 함께 전달한다 (§16-17).
/// </summary>
public sealed class CadAreaObjectDto
{
    public CadAreaObjectDto(string handle, SupportedAreaGeometryType geometryType, string layerName, double rawArea, bool isClosed)
    {
        Handle = handle;
        GeometryType = geometryType;
        LayerName = layerName;
        RawArea = rawArea;
        IsClosed = isClosed;
    }

    /// <summary>AutoCAD Handle 문자열. 향후 "CAD에서 원본 객체 찾기" 기능에 사용한다 (§71).</summary>
    public string Handle { get; }

    public SupportedAreaGeometryType GeometryType { get; }

    public string LayerName { get; }

    /// <summary>
    /// Drawing Unit 기준 원본 값(제곱). IsClosed가 false면 의미 없는 값(0)이다.
    /// AutoCAD 쪽에서 Area를 읽다가 예외가 나거나 결과가 비정상이면 NaN을 담아 보낸다 - Core가
    /// "닫혀 있는데 면적이 비정상"(§17)을 판단할 수 있는 유일한 신호다. AutoCAD 예외 자체는
    /// Core가 재현할 수 없으므로 Handler가 이미 판단해서 넘긴다.
    /// </summary>
    public double RawArea { get; }

    public bool IsClosed { get; }
}
