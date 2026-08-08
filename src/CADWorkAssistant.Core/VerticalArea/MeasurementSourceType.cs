namespace CADWorkAssistant.Core.VerticalArea;

/// <summary>
/// 기준 길이가 어디서 왔는지 - Vertical Area/Parapet 둘 다 필요하다 (Milestone 4 §16-19).
/// CAD Handle 유무나 재검산 가능 여부가 이 값에 따라 달라지므로 provenance의 핵심 정보다.
/// </summary>
public enum MeasurementSourceType
{
    /// <summary>지금 막 AutoCAD에서 새로 선택했다.</summary>
    CadSelection,

    /// <summary>Length 도구에서 이미 측정해둔 값을 재사용했다.</summary>
    ExistingMeasurement,

    /// <summary>사용자가 직접 숫자를 입력했다 - AutoCAD 객체와 연결되지 않는다.</summary>
    Manual
}
