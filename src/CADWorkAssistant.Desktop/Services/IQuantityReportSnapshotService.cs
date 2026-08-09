using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>Milestone 10 §44 - Project/QuantityRecord/Verification/Review를 Persistence에서 새로
/// 읽어온 한 시점의 스냅샷. Excel/PDF Export Coordinator가 둘 다 이 스냅샷 하나를
/// <see cref="CADWorkAssistant.Documents.Reports.QuantityReportModelBuilder"/>에 그대로 넘겨 같은
/// Project+Scope에 대해 항상 같은 모델을 만든다(Cross-format consistency, §144-148).</summary>
public sealed class QuantityReportSnapshot
{
    public QuantityReportSnapshot(
        Project project,
        IReadOnlyList<QuantityRecord> records,
        IReadOnlyDictionary<string, QuantityVerificationResult> verifications,
        IReadOnlyDictionary<string, QuantityReview> reviews)
    {
        Project = project;
        Records = records;
        Verifications = verifications;
        Reviews = reviews;
    }

    public Project Project { get; }

    public IReadOnlyList<QuantityRecord> Records { get; }

    /// <summary>Key: QuantityRecordId.</summary>
    public IReadOnlyDictionary<string, QuantityVerificationResult> Verifications { get; }

    /// <summary>Key: QuantityRecordId.</summary>
    public IReadOnlyDictionary<string, QuantityReview> Reviews { get; }
}

/// <summary>
/// Milestone 9에서는 QuantityExcelExportCoordinator 안에 이 조회 로직(Project+QuantityRecord를
/// Persistence에서 새로 읽고, IQuantityVerificationCoordinator로 최신 검산/검토 상태를 재사용)이
/// 그대로 있었다. Milestone 10에서 PDF Coordinator가 정확히 같은 조회를 다시 필요로 하면서 이
/// 서비스로 뽑아냈다 - "generic mega-export framework"(§44)를 만드는 게 아니라, 이미 존재하던
/// 중복을 없애는 정상적인 리팩터링이다.
/// </summary>
public interface IQuantityReportSnapshotService
{
    Task<QuantityReportSnapshot> LoadAsync(string projectId);
}
