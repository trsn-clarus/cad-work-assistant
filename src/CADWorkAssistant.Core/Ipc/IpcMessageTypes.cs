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

    /// <summary>현재 도면의 Extents/객체 수/Layer 수 - Drawing Navigation의 최소 기반 (Milestone 5 §64).</summary>
    public const string GetDrawingOverview = "GetDrawingOverview";

    /// <summary>도면 전체가 화면에 보이도록 View를 맞춘다 (Milestone 5 §21).</summary>
    public const string ZoomExtents = "ZoomExtents";

    /// <summary>주어진 Bounds가 화면에 보이도록 View를 맞춘다 - "선택 영역 보기" 등 Bounds 출처를
    /// 가리지 않는다 (Milestone 5 §23).</summary>
    public const string ZoomToBounds = "ZoomToBounds";

    /// <summary>Window/Crossing 영역으로 객체를 선택하고 원본 목록을 반환한다 (Milestone 5 §26-30).</summary>
    public const string SelectDrawingObjects = "SelectDrawingObjects";

    /// <summary>주어진 Handle들만 보이게 하고 나머지는 임시로 숨긴다 (Milestone 5 §32-33).</summary>
    public const string IsolateObjects = "IsolateObjects";

    /// <summary>도면의 전체 Layer 목록과 On/Off/Freeze/Lock/Plottable 상태를 반환한다 (Milestone 5 §38-39).</summary>
    public const string GetLayers = "GetLayers";

    /// <summary>지정한 Layer들의 On/Off 상태를 바꾼다 - 개별 토글과 "선택 Layer만 보기" 둘 다 이 명령
    /// 하나로 표현한다 (Milestone 5 §37, §41).</summary>
    public const string SetLayerVisibility = "SetLayerVisibility";

    /// <summary>IsolateObjects/SetLayerVisibility로 CWA가 임시로 바꾼 표시 상태를 원래대로 되돌린다
    /// (Milestone 5 §35, §45-46).</summary>
    public const string RestoreVisibility = "RestoreVisibility";

    /// <summary>선택한 객체들을 WBLOCK으로 새 DWG에 저장한다. 원본은 수정하지 않는다 (Milestone 5 §49-52).</summary>
    public const string ExportSelection = "ExportSelection";

    // 향후 Milestone에서 추가될 예정: PlotDrawing 등.
    // 여기 상수만 추가하고 Handlers/ 아래 새 IIpcRequestHandler 구현을 등록하면 된다.
}
