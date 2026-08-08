namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// Area 결과 테이블의 한 행. 표시 전용 - 계산 로직을 갖지 않는다 (Length의 LengthObjectRow와 동일한 역할).
/// 유효한 항목만 담는다 - 열림/미지원/비정상형상으로 제외된 항목은 ExcludedSummary로 따로 요약한다
/// (§50 mockup과 동일하게 메인 테이블에는 계산된 값만 보인다).
/// </summary>
public sealed class AreaObjectRow
{
    public AreaObjectRow(string handle, string geometryType, string layer, string areaDisplay)
    {
        Handle = handle;
        GeometryType = geometryType;
        Layer = layer;
        AreaDisplay = areaDisplay;
    }

    public string Handle { get; }
    public string GeometryType { get; }
    public string Layer { get; }
    public string AreaDisplay { get; }
}
