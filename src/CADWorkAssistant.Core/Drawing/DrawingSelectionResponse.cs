using System.Collections.Generic;

namespace CADWorkAssistant.Core.Drawing;

/// <summary>
/// `SelectDrawingObjects` IPC 응답 payload. AutoCAD Plugin은 원본 목록만 준다 - 요약(개수/타입별
/// 집계/Layer 집계/합산 Bounds)은 Core의 <see cref="DrawingSelectionSummary"/>가 계산한다.
/// </summary>
public sealed class DrawingSelectionResponse
{
    public DrawingSelectionResponse(IReadOnlyList<CadSelectedObjectDto> objects)
    {
        Objects = objects;
    }

    public IReadOnlyList<CadSelectedObjectDto> Objects { get; }
}
