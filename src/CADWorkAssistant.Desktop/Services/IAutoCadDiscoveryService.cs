using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>실행 중인 AutoCAD 프로세스를 찾고, 각각에 Plugin이 응답하는지 확인한다 (§21).</summary>
public interface IAutoCadDiscoveryService
{
    Task<IReadOnlyList<AutoCadInstanceCandidate>> DiscoverAsync(CancellationToken cancellationToken);
}
