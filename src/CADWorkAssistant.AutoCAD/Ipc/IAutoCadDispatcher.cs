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

    /// <summary>
    /// Editor.GetSelection처럼 사용자 인터랙션을 기다리는 인터랙티브 명령용. 문서의 Command Context
    /// 안에서 실행되어 Selection/Prompt API를 안전하게 쓸 수 있다 (Milestone 2 §15, ExecuteInCommandContextAsync
    /// 실존을 리플렉션으로 확인함).
    /// </summary>
    Task<T> InvokeInCommandContextAsync<T>(Func<T> operation, CancellationToken cancellationToken);
}
