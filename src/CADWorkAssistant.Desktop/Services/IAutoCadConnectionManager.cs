using System.ComponentModel;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>
/// AutoCAD 연결의 전체 lifecycle(Discover/Connect/Heartbeat/Reconnect)을 관리한다.
/// ViewModel은 이 인터페이스만 보고, Named Pipe/Discovery 세부 구현은 알지 못한다 (§22, §37).
/// </summary>
public interface IAutoCadConnectionManager : INotifyPropertyChanged, IDisposable
{
    CadConnectionState State { get; }

    AutoCadInstanceInfo? Instance { get; }

    DrawingContext? Drawing { get; }

    IReadOnlyList<AutoCadInstanceCandidate> AvailableInstances { get; }

    int? SelectedProcessId { get; }

    /// <summary>백그라운드 Discover/Connect/Heartbeat 루프를 시작한다. Dispose할 때까지 계속 돈다.</summary>
    void Start();

    /// <summary>AutoCAD Instance가 여러 개일 때 사용자가 하나를 고른다 (§23, §36).</summary>
    Task SelectInstanceAsync(int processId, CancellationToken cancellationToken);
}
