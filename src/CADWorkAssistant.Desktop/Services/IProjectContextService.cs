using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>
/// "지금 어떤 Project가 열려 있는가"와 그 Project에 속한 QuantityRecord/ActivityRecord를 한 곳에서
/// 관리한다 (Milestone 6 §19-22) - 여러 ViewModel이 각자 프로젝트 상태를 들고 있지 않게 한다.
/// Project가 하나도 열려 있지 않아도("빠른 세션", §21) 측정 자체는 계속할 수 있다 - 그 경우
/// QuantityRecords/Activity는 메모리에만 있고 DB에 저장되지 않는다.
/// </summary>
public interface IProjectContextService
{
    Project? CurrentProject { get; }

    /// <summary>LastOpenedAt DESC. Project 전환 UI가 그대로 바인딩한다.</summary>
    ObservableCollection<Project> RecentProjects { get; }

    /// <summary>CurrentProject가 바뀌면(생성/전환) 그 프로젝트의 저장된 값으로 다시 채워진다.
    /// Project가 없으면(빠른 세션) 세션 동안만 쌓이는 메모리 전용 컬렉션이다.</summary>
    ObservableCollection<QuantityRecord> QuantityRecords { get; }

    ObservableCollection<ActivityRecord> Activity { get; }

    event EventHandler? CurrentProjectChanged;

    /// <summary>앱 시작 시 1회 - 최근 프로젝트 목록을 불러온다. DB 파일이 없으면 새로 만든다.</summary>
    Task InitializeAsync();

    Task<Project> CreateProjectAsync(string name, string? client, string? site, string? description);

    /// <summary>기존 프로젝트를 열거나(최근 목록에서 선택) 전환한다 - LastOpenedAt이 갱신된다.</summary>
    Task OpenProjectAsync(string projectId);

    /// <summary>Milestone 13 §11-12 - Id/CreatedAt은 바뀌지 않는다. projectId가 CurrentProject와
    /// 다를 수도 있다(Projects 목록에서 지금 열려 있지 않은 프로젝트를 바로 편집하는 경우).</summary>
    Task UpdateProjectAsync(string projectId, string name, string? client, string? site, string? description);

    /// <summary>Milestone 13 §7-9 - RecentProjects(최근 20개)보다 많은 전체 목록. Projects Workspace의
    /// 검색/정렬은 이 결과를 메모리에서 처리한다(이 규모에서 SQL 쿼리 빌더는 과함, §145).</summary>
    Task<IReadOnlyList<Project>> GetAllProjectsAsync();

    /// <summary>Milestone 13 §17-19 - CurrentProject 여부와 무관하게 특정 프로젝트의 등록 도면
    /// 목록을 조회한다(목록에서 미리보기만 하고 아직 열지 않은 프로젝트도 조회 가능해야 한다).</summary>
    Task<IReadOnlyList<DrawingFile>> GetDrawingFilesAsync(string projectId);

    /// <summary>Milestone 13 §7 - Projects 목록 전체의 프로젝트별 등록 도면 개수(ProjectId → count).</summary>
    Task<IReadOnlyDictionary<string, int>> GetDrawingFileCountsAsync();

    /// <summary>Milestone 13 §32-37 - Excel/PDF 보고서/도면 PDF/DWG Export 이력을 전부 합쳐 보여주는
    /// Output History. limit로 한 번에 전부 불러오지 않는다(§143).</summary>
    Task<IReadOnlyList<ExportRecord>> GetExportRecordsAsync(string projectId, int limit);

    /// <summary>Milestone 13 §38-40 - Dashboard의 실시간 Activity(현재 프로젝트, 최대 100개)와
    /// 별개로, Projects Workspace에서 임의 프로젝트의 최근 활동을 조회한다.</summary>
    Task<IReadOnlyList<ActivityRecord>> GetActivityForProjectAsync(string projectId, int limit);

    /// <summary>Milestone 13 §41 - Project Overview용 수량 요약(전체/검토 완료/확인 필요/오류).</summary>
    Task<ProjectQuantitySummary> GetQuantitySummaryAsync(string projectId);

    /// <summary>Milestone 13 §23-25 - 등록된 DWG 참조가 이동/삭제됐을 때 새 경로로 다시 연결한다.
    /// 과거 QuantityRecord의 SourceDrawing snapshot은 건드리지 않는다.</summary>
    Task RelinkDrawingFileAsync(string projectId, string drawingFileId, string newFullPath, string newFileName, string? newDrawingUnit);

    /// <summary>QuantityRecord 저장 + 그에 대응하는 ActivityRecord 기록을 하나의 단위로 처리한다
    /// (§45-46). Project가 열려 있으면 DB에 트랜잭션으로 남고, 아니면 메모리에만 반영된다.</summary>
    Task AddQuantityRecordAsync(QuantityRecord record, string activityTitle, string? activityDescription);

    Task UpdateQuantityRecordDescriptionAsync(string id, string description);

    Task DeleteQuantityRecordAsync(string id);

    /// <summary>AutoCAD 연결이 도면을 열 때마다 자동으로 호출된다 (§34-37) - 파일을 복사하지 않고
    /// 경로만 참조로 남긴다. 같은 경로면 새 행 대신 갱신된다.</summary>
    Task RegisterDrawingFileAsync(string fullPath, string fileName, string? drawingUnit);

    Task AddExportRecordAsync(string? sourceDrawing, string targetFile, int objectCount, string? description);

    /// <summary>Milestone 9 - Excel 수량산출서 저장 이력을 남긴다. AddExportRecordAsync와 별도 메서드로
    /// 둔 이유는 ExportRecord.ExportType/Activity 제목이 서로 다르고, 나중에 Excel만의 필드가
    /// 늘어나도 DWG Export 경로에 영향을 주지 않기 위해서다 (AddQuantityRecordAsync가 이미
    /// 측정 종류별로 호출부가 activityTitle을 직접 정하는 것과 같은 이유).</summary>
    Task AddExcelExportRecordAsync(string targetFile, int recordCount, string scopeDescription);

    /// <summary>Milestone 10 - PDF 산출근거서 저장 이력을 남긴다. AddExcelExportRecordAsync와 같은
    /// 이유로 별도 메서드다.</summary>
    Task AddPdfExportRecordAsync(string targetFile, int recordCount, string scopeDescription);

    /// <summary>Milestone 11 - AutoCAD Plot으로 만든 도면 PDF 저장 이력을 남긴다. 수량 보고서
    /// PDF(AddPdfExportRecordAsync)와 완전히 다른 서브시스템의 산출물이라 ExportType과 Activity
    /// 제목을 분리한다(§166).</summary>
    Task AddDrawingPdfExportRecordAsync(string targetFile, int pageCount, string scopeDescription);
}
