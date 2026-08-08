using System;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// <see cref="Autodesk.AutoCAD.ApplicationServices.DocumentCollection.ExecuteInApplicationContext"/>를
/// Task 기반으로 감싼 것. 이 API는 실제 설치된 AutoCAD 2024의 accoremgd.dll에서 리플렉션으로 존재를
/// 확인한 뒤 사용했다 (§10 - 추측 금지). Read-only 상태 조회(Milestone 1)에 쓰기 충분하며, 향후
/// 인터랙티브 명령(Selection 등)이 필요해지면 ExecuteInCommandContextAsync를 별도로 추가한다 (§11).
/// </summary>
public sealed class AutoCadDispatcher : IAutoCadDispatcher
{
    public Task<T> InvokeAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.IsCancellationRequested)
        {
            tcs.TrySetCanceled(cancellationToken);
            return tcs.Task;
        }

        var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        tcs.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);

        try
        {
            Application.DocumentManager.ExecuteInApplicationContext(
                _ =>
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
                },
                null);
        }
        catch (Exception ex)
        {
            // AutoCAD가 이미 종료 중이라 큐잉 자체가 실패하는 경우 등.
            tcs.TrySetException(ex);
        }

        return tcs.Task;
    }
}
