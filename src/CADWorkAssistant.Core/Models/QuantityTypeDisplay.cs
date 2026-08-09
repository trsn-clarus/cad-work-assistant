namespace CADWorkAssistant.Core.Models;

/// <summary>
/// QuantityRecord.Type의 내부 identifier("Length"/"Area"/"VerticalArea"/"Parapet")를 사용자가 보는
/// 한국어 표시명/표시 정밀도로 바꾼다(Milestone 9 §13, §18-19). 내부 identifier는 필터/정렬/DB 저장에
/// 계속 쓰고, 화면·문서에 노출하는 표시명만 이 클래스를 거친다 - 새 Quantity 종류가 생기면 이 한
/// 곳만 늘리면 된다.
/// 소수점 자리수는 이미 각 측정 도구 ViewModel이 실제로 쓰던 값을 그대로 옮겼다 - Length는
/// <see cref="Length.LengthFormatter.DefaultDecimalPlaces"/>(3), Area는
/// <see cref="Area.AreaFormatter.DefaultDecimalPlaces"/>(2), VerticalArea/Parapet은 m²인데도
/// VerticalAreaWorkflowViewModel/ParapetWorkflowViewModel이 명시적으로 3자리를 써왔다(면적치고
/// 이례적이지만 새 정책이 아니라 기존 동작을 그대로 반영한 것 - Excel에서 Area만 2자리가 되는 게
/// 오히려 새 불일치를 만드는 것보다, 지금까지 화면에 보여주던 자리수와 통일하는 쪽을 선택했다).
/// </summary>
public static class QuantityTypeDisplay
{
    public static string DisplayName(string type) => type switch
    {
        "Length" => "길이",
        "Area" => "면적",
        "VerticalArea" => "수직면적",
        "Parapet" => "파라펫",
        _ => type
    };

    public static int DecimalPlaces(string type) => type switch
    {
        "Length" => Length.LengthFormatter.DefaultDecimalPlaces,
        "Area" => Area.AreaFormatter.DefaultDecimalPlaces,
        "VerticalArea" => 3,
        "Parapet" => 3,
        _ => 2
    };
}
