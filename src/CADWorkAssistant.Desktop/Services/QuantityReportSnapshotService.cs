using System;
using System.Threading.Tasks;
using CADWorkAssistant.Persistence;

namespace CADWorkAssistant.Desktop.Services;

public sealed class QuantityReportSnapshotService : IQuantityReportSnapshotService
{
    private readonly ProjectDataService _dataService;
    private readonly IQuantityVerificationCoordinator _verificationCoordinator;

    public QuantityReportSnapshotService(ProjectDataService dataService, IQuantityVerificationCoordinator verificationCoordinator)
    {
        _dataService = dataService;
        _verificationCoordinator = verificationCoordinator;
    }

    public async Task<QuantityReportSnapshot> LoadAsync(string projectId)
    {
        using var connection = _dataService.Database.OpenConnection();
        var project = await _dataService.Projects.FindByIdAsync(projectId, connection)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");
        var records = await _dataService.QuantityRecords.GetByProjectAsync(projectId, connection);

        // Verification/Review는 이미 같은 정책(fresh read, 캐시 없음)으로 동작하는 기존 Coordinator를
        // 재사용한다(Milestone 7) - 검산 Snapshot 역직렬화 로직을 여기서 다시 만들지 않는다.
        var snapshotSet = await _verificationCoordinator.LoadForProjectAsync(projectId);

        return new QuantityReportSnapshot(project, records, snapshotSet.Verifications, snapshotSet.Reviews);
    }
}
