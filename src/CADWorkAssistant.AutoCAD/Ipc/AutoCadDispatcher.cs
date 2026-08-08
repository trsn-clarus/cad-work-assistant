using System;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// <see cref="Autodesk.AutoCAD.ApplicationServices.DocumentCollection.ExecuteInApplicationContext"/>와
/// <see cref="Autodesk.AutoCAD.ApplicationServices.DocumentCollection.ExecuteInCommandContextAsync"/>를
/// Task 기반으로 감싼 것. 둘 다 실제 설치된 AutoCAD 2024의 accoremgd.dll에서 리플렉션으로 존재를
/// 확인한 뒤 사용했다 (§10, Milestone 2 §15 - 추측 금지). ApplicationContext는 Read-only 상태 조회에,
/// CommandContext는 Editor.GetSelection처럼 사용자 입력을 기다리는 인터랙티브 작업에 쓴다.
/// </summary>
public sealed class AutoCadDispatcher : IAutoCadDispatcher
{
    public Task<T> InvokeAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        var tcs = CreateLinkedTcs<T>(cancellationToken, out var alreadyCancelled);
        if (alreadyCancelled)
        {
            return tcs.Task;
        }

        try
        {
            Application.DocumentManager.ExecuteInApplicationContext(
                _ => Complete(tcs, operation, cancellationToken),
                null);
        }
        catch (Exception ex)
        {
            // AutoCAD가 이미 종료 중이라 큐잉 자체가 실패하는 경우 등.
            tcs.TrySetException(ex);
        }

        return tcs.Task;
    }

    public Task<T> InvokeInCommandContextAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        var tcs = CreateLinkedTcs<T>(cancellationToken, out var alreadyCancelled);
        if (alreadyCancelled)
        {
            return tcs.Task;
        }

        try
        {
            Application.DocumentManager.ExecuteInCommandContextAsync(
                _ =>
                {
                    Complete(tcs, operation, cancellationToken);
                    return Task.CompletedTask;
                },
                null);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }

        return tcs.Task;
    }

    private static TaskCompletionSource<T> CreateLinkedTcs<T>(CancellationToken cancellationToken, out bool alreadyCancelled)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.IsCancellationRequested)
        {
            tcs.TrySetCanceled(cancellationToken);
            alreadyCancelled = true;
            return tcs;
        }

        var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        tcs.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);

        alreadyCancelled = false;
        return tcs;
    }

    private static void Complete<T>(TaskCompletionSource<T> tcs, Func<T> operation, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            tcs.TrySetCanceled(cancellationToken);
            return;
        }

        try
        {
            tcs.TrySetResult(operation());
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }
}
