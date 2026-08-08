namespace CADWorkAssistant.Core.Area;

/// <summary>
/// 선택된 객체 하나가 최종 합산에 들어갔는지, 아니라면 왜 제외됐는지 (Milestone 3 §8, §16-18, §42).
/// </summary>
public enum AreaObjectStatus
{
    /// <summary>닫혀 있고 면적을 정상적으로 읽어 합산에 포함됐다.</summary>
    Valid,

    /// <summary>면적 산출 Geometry 종류이지만 닫혀 있지 않다 (예: 열린 Polyline, Ellipse 호) (§16).</summary>
    Open,

    /// <summary>면적 산출을 지원하지 않는 객체 타입이다 (예: Hatch, Text, Line) (§42).</summary>
    Unsupported,

    /// <summary>닫혀 있지만 면적 값을 신뢰할 수 없다 - AutoCAD가 예외를 던졌거나, 결과가 0/NaN/Infinity다 (§17).</summary>
    InvalidGeometry
}
