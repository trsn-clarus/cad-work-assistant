using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Persistence.Repositories;

namespace CADWorkAssistant.Persistence;

/// <summary>
/// Repository들을 조합해 트랜잭션 경계를 만든다 (Milestone 6 §45-46) - 지금까지 확인된 유일한
/// 교차 테이블 원자성 요구는 "산출내역 추가"가 QuantityRecord와 ActivityRecord를 동시에 남기는
/// 것이다: 하나만 저장되고 다른 하나가 실패하면 안 된다. Repository 자체는 커넥션을 안 들고 있는
/// 상태 없는(stateless) 클래스라 이 서비스가 커넥션/트랜잭션을 열고 각 Repository의 메서드에
/// 넘겨주는 조립 지점 역할을 한다.
/// </summary>
public sealed class ProjectDataService
{
    private readonly CadWorkAssistantDatabase _database;
    private readonly IProjectRepository _projects;
    private readonly IQuantityRecordRepository _quantityRecords;
    private readonly IActivityRepository _activity;
    private readonly IDrawingFileRepository _drawingFiles;
    private readonly IExportRecordRepository _exportRecords;
    private readonly IRecentMeasurementRepository _recentMeasurements;

    public ProjectDataService(
        CadWorkAssistantDatabase database,
        IProjectRepository? projects = null,
        IQuantityRecordRepository? quantityRecords = null,
        IActivityRepository? activity = null,
        IDrawingFileRepository? drawingFiles = null,
        IExportRecordRepository? exportRecords = null,
        IRecentMeasurementRepository? recentMeasurements = null)
    {
        _database = database;
        _projects = projects ?? new SqliteProjectRepository();
        _quantityRecords = quantityRecords ?? new SqliteQuantityRecordRepository();
        _activity = activity ?? new SqliteActivityRepository();
        _drawingFiles = drawingFiles ?? new SqliteDrawingFileRepository();
        _exportRecords = exportRecords ?? new SqliteExportRecordRepository();
        _recentMeasurements = recentMeasurements ?? new SqliteRecentMeasurementRepository();
    }

    /// <summary>단일 테이블 조회/갱신처럼 트랜잭션 묶음이 필요 없는 호출은 호출부가 직접
    /// <c>using var connection = Database.OpenConnection();</c>로 연결을 열어 각 Repository
    /// 프로퍼티에 넘긴다.</summary>
    public CadWorkAssistantDatabase Database => _database;

    public IProjectRepository Projects => _projects;

    public IQuantityRecordRepository QuantityRecords => _quantityRecords;

    public IActivityRepository Activity => _activity;

    public IDrawingFileRepository DrawingFiles => _drawingFiles;

    public IExportRecordRepository ExportRecords => _exportRecords;

    public IRecentMeasurementRepository RecentMeasurements => _recentMeasurements;

    /// <summary>산출내역 저장 + 활동 기록을 하나의 트랜잭션으로 묶는다 - 두 INSERT가 같이 성공하거나
    /// 같이 실패한다.</summary>
    public async Task AddQuantityRecordWithActivityAsync(QuantityRecord record, ActivityRecord activity)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            await _quantityRecords.InsertAsync(record, connection, transaction);
            await _activity.InsertAsync(activity, connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>새 Project 생성 + "프로젝트 생성" 활동 기록을 하나의 트랜잭션으로 묶는다.</summary>
    public async Task CreateProjectWithActivityAsync(Project project, ActivityRecord activity)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            await _projects.InsertAsync(project, connection, transaction);
            await _activity.InsertAsync(activity, connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
