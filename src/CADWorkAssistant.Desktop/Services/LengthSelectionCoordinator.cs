using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Length;

namespace CADWorkAssistant.Desktop.Services;

internal enum LengthCoordinatorOutcomeKind
{
    Selected,
    Empty,
    Cancelled,
    Error
}

/// <summary>
/// AutoCadDispatcher 쪽의 SelectionOutcome&lt;T&gt;와 같은 이유로 같은 모양을 쓴다 (Milestone 2/3) -
/// 하나의 비동기 작업에서 나올 수 있는 여러 종류의 종결 상태를 하나의 타입으로 감싼다.
/// </summary>
internal sealed class LengthCoordinatorOutcome
{
    private LengthCoordinatorOutcome(LengthCoordinatorOutcomeKind kind, LengthMeasurementResult? result, IpcError? error)
    {
        Kind = kind;
        Result = result;
        Error = error;
    }

    public LengthCoordinatorOutcomeKind Kind { get; }

    public LengthMeasurementResult? Result { get; }

    public IpcError? Error { get; }

    public static LengthCoordinatorOutcome Selected(LengthMeasurementResult result) => new(LengthCoordinatorOutcomeKind.Selected, result, null);

    public static LengthCoordinatorOutcome Empty() => new(LengthCoordinatorOutcomeKind.Empty, null, null);

    public static LengthCoordinatorOutcome Cancelled() => new(LengthCoordinatorOutcomeKind.Cancelled, null, null);

    public static LengthCoordinatorOutcome Failed(IpcError error) => new(LengthCoordinatorOutcomeKind.Error, null, error);
}

/// <summary>
/// Vertical Area/Parapet이 "CAD에서 새로 선택"할 때 쓰는 공유 헬퍼 (Milestone 4 §5-6, §50-51) - 새
/// AutoCAD IPC 명령을 만들지 않고 Milestone 2의 SelectLengthObjects를 그대로 재사용한다. 두
/// ViewModel이 똑같은 "IPC 요청 → LengthSelectionResponse → LengthAggregationService.Aggregate"
/// 절차를 각자 구현하면 명백한 중복이라 여기 하나로 모았다 - Event Bus/MediatR 같은 무거운 방식
/// 없이, 이 규모에 맞는 정적 헬퍼 하나로 충분하다 (§51).
/// </summary>
internal static class LengthSelectionCoordinator
{
    public static async Task<LengthCoordinatorOutcome> SelectFromCadAsync(
        IAutoCadConnectionManager connectionManager,
        string? drawingName,
        CancellationToken cancellationToken)
    {
        var response = await connectionManager
            .SendRequestAsync(IpcMessageTypes.SelectLengthObjects, payload: null, cancellationToken)
            .ConfigureAwait(true);

        if (!response.Success)
        {
            return response.Error!.Code == IpcErrorCode.SelectionCancelled
                ? LengthCoordinatorOutcome.Cancelled()
                : LengthCoordinatorOutcome.Failed(response.Error!);
        }

        var selection = response.DeserializePayload<LengthSelectionResponse>();
        if (selection is null)
        {
            return LengthCoordinatorOutcome.Failed(new IpcError(IpcErrorCode.InternalError, "Empty response payload."));
        }

        if (selection.Objects.Count == 0 && selection.ExcludedObjectTypeNames.Count == 0)
        {
            return LengthCoordinatorOutcome.Empty();
        }

        var result = LengthAggregationService.Aggregate(selection, drawingName, System.DateTimeOffset.Now);
        return LengthCoordinatorOutcome.Selected(result);
    }
}
