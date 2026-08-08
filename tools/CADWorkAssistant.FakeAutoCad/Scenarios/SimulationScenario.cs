using System.Collections.Generic;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Length;

namespace CADWorkAssistant.FakeAutoCad.Scenarios;

/// <summary>어떤 IPC 요청에 대해 SelectLengthObjects Handler가 어떻게 행동할지 정의한다 (Milestone 2 §8).</summary>
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
/// SelectLengthObjects 양쪽에 동시에 쓰인다.
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

    public SelectionBehavior Behavior { get; init; } = SelectionBehavior.ReturnObjects;

    public IReadOnlyList<CadLengthObjectDto> Objects { get; init; } = System.Array.Empty<CadLengthObjectDto>();

    public IReadOnlyList<string> ExcludedObjectTypeNames { get; init; } = System.Array.Empty<string>();
}
