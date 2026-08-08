namespace CADWorkAssistant.Core.Ipc;

/// <summary>
/// 지원하는 IPC 요청 종류. 문자열 상수를 쓰는 이유: 새 명령을 추가할 때 프로토콜 버전을 올리지 않아도 되고,
/// 로그에 그대로 남겨도 읽기 쉽다 (docs/AUTOCAD_INTEGRATION.md §5).
/// </summary>
public static class IpcMessageTypes
{
    public const string Ping = "Ping";
    public const string GetApplicationInfo = "GetApplicationInfo";
    public const string GetDrawingContext = "GetDrawingContext";

    /// <summary>사용자가 AutoCAD에서 선택한 Line/Polyline/Arc의 원본 길이 데이터를 반환한다 (Milestone 2 §12).</summary>
    public const string SelectLengthObjects = "SelectLengthObjects";

    /// <summary>사용자가 AutoCAD에서 선택한 Polyline/Circle/Ellipse/Region의 원본 면적 데이터를 반환한다 (Milestone 3 §10).</summary>
    public const string SelectAreaObjects = "SelectAreaObjects";

    // 향후 Milestone에서 추가될 예정: SetLayerVisibility, ExportSelection, PlotDrawing 등.
    // 여기 상수만 추가하고 Handlers/ 아래 새 IIpcRequestHandler 구현을 등록하면 된다.
}
