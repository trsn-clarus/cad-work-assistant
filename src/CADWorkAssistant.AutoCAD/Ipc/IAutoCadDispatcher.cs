using System;
using System.Threading;
using System.Threading.Tasks;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// Named Pipe 요청은 백그라운드 스레드에서 들어오지만, AutoCAD Managed API(Document/Database/Editor)는
/// AutoCAD의 Application Context에서만 안전하게 호출할 수 있다 (docs/AUTOCAD_INTEGRATION.md, Milestone 1 §9-10).
/// 모든 AutoCAD API 호출은 예외 없이 이 Dispatcher를 통해서만 이뤄져야 한다.
/// </summary>
public interface IAutoCadDispatcher
{
    Task<T> InvokeAsync<T>(Func<T> operation, CancellationToken cancellationToken);
}
