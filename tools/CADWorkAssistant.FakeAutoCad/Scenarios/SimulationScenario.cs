using System.Collections.Generic;
using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Length;

namespace CADWorkAssistant.FakeAutoCad.Scenarios;

/// <summary>
/// 어떤 IPC 요청에 대해 SelectLengthObjects/SelectAreaObjects Handler가 어떻게 행동할지 정의한다
/// (Milestone 2 §8). Length/Area 둘 다 같은 다섯 가지 행동 패턴(정상 응답/취소/멈춤/연결끊김/오류)만
/// 있으면 충분해서 하나의 enum을 공유한다 - Length/AreaBehavior를 독립된 필드로 두면 같은 시나리오
/// 안에서 두 기능이 다르게 행동하는 조합도 표현할 수 있다.
/// </summary>
public enum SelectionBehavior
{
    /// <summary>Objects/ExcludedObjectTypeNames를 그대로 응답한다 (정상 케이스, 빈 선택도 포함).</summary>
    ReturnObjects,

    /// <summary>사용자가 Esc로 취소한 것처럼 SelectionCancelled 오류를 응답한다.</summary>
    Cancelled,

    /// <summary>영원히 응답하지 않는다 - 클라이언트 쪽 Request Timeout을 검증한다.</summary>
    HangForever,

    /// <summary>연결은 받아들이지만 응답 전에 Pipe를 끊는다 - 연결 끊김 처리를 검증한다.</summary>
    DisconnectBeforeResponding,

    /// <summary>AutoCAD 내부 오류가 난 것처럼 ApiExecutionFailed를 응답한다.</summary>
    ReturnError
}

/// <summary>
/// FakeAutoCAD 프로세스 하나가 시작 시점에 갖는 고정 시나리오. GetDrawingContext와
/// SelectLengthObjects/SelectAreaObjects 전부에 동시에 쓰인다. Length와 Area는 서로 다른 Objects
/// 목록/Behavior를 가질 수 있다 - 같은 도면(Drawing/Unit)에서 두 기능을 각각 검증하기 때문이다.
/// </summary>
public sealed class SimulationScenario
{
    public required string Name { get; init; }

    public string DrawingDisplayName { get; init; } = "School_Roof.dwg";

    public string? DrawingFullPath { get; init; } = @"C:\Simulated\School_Roof.dwg";

    public bool IsSaved { get; init; } = true;

    public bool IsReadOnly { get; init; }

    public string Layout { get; init; } = "Model";

    public DrawingUnit Unit { get; init; } = DrawingUnit.Millimeters;

    public int DocumentCount { get; init; } = 1;

    public SelectionBehavior LengthBehavior { get; init; } = SelectionBehavior.ReturnObjects;

    public IReadOnlyList<CadLengthObjectDto> Objects { get; init; } = System.Array.Empty<CadLengthObjectDto>();

    public IReadOnlyList<string> ExcludedObjectTypeNames { get; init; } = System.Array.Empty<string>();

    public SelectionBehavior AreaBehavior { get; init; } = SelectionBehavior.ReturnObjects;

    public IReadOnlyList<CadAreaObjectDto> AreaObjects { get; init; } = System.Array.Empty<CadAreaObjectDto>();

    public IReadOnlyList<string> AreaExcludedObjectTypeNames { get; init; } = System.Array.Empty<string>();

    // --- Drawing Navigation (Milestone 5) ---

    /// <summary>SelectDrawingObjects의 Window/Crossing 선택 결과 - GetDrawingOverview/ZoomExtents가
    /// 계산하는 "전체 도면"도 이 목록을 ModelSpace 전체라고 취급한다.</summary>
    public IReadOnlyList<CadSelectedObjectDto> DrawingObjects { get; init; } = System.Array.Empty<CadSelectedObjectDto>();

    public SelectionBehavior DrawingSelectionBehavior { get; init; } = SelectionBehavior.ReturnObjects;

    public IReadOnlyList<CadLayerDto> Layers { get; init; } = System.Array.Empty<CadLayerDto>();

    /// <summary>ExportSelection이 AutoCAD 내부 오류로 실패한 것처럼 흉내낸다 (§68 ExportSelectionError).</summary>
    public bool ExportShouldFail { get; init; }
}
