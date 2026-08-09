using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Persistence;

namespace CADWorkAssistant.Desktop.Services;

public sealed class ProjectContextService : ObservableObject, IProjectContextService
{
    private const int RecentProjectLimit = 20;
    private const int ActivityLimit = 100;

    private readonly ProjectDataService _dataService;
    private Project? _currentProject;

    public ProjectContextService(ProjectDataService dataService)
    {
        _dataService = dataService;
        RecentProjects = new ObservableCollection<Project>();
        QuantityRecords = new ObservableCollection<QuantityRecord>();
        Activity = new ObservableCollection<ActivityRecord>();
    }

    public Project? CurrentProject
    {
        get => _currentProject;
        private set => SetProperty(ref _currentProject, value);
    }

    public ObservableCollection<Project> RecentProjects { get; }

    public ObservableCollection<QuantityRecord> QuantityRecords { get; }

    public ObservableCollection<ActivityRecord> Activity { get; }

    public event EventHandler? CurrentProjectChanged;

    public async Task InitializeAsync()
    {
        using var connection = _dataService.Database.OpenConnection();
        var recent = await _dataService.Projects.GetRecentAsync(RecentProjectLimit, connection);
        RecentProjects.Clear();
        foreach (var project in recent)
        {
            RecentProjects.Add(project);
        }
    }

    public async Task<Project> CreateProjectAsync(string name, string? client, string? site, string? description)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(
            id: Guid.NewGuid().ToString("N"),
            name: name,
            createdAt: now,
            updatedAt: now,
            lastOpenedAt: now,
            client: client,
            site: site,
            description: description);

        var activity = new ActivityRecord(
            id: Guid.NewGuid().ToString("N"),
            projectId: project.Id,
            activityType: "ProjectCreated",
            title: "프로젝트 생성",
            description: name,
            createdAt: now);

        await _dataService.CreateProjectWithActivityAsync(project, activity);

        RecentProjects.Insert(0, project);
        await SwitchToAsync(project);
        return project;
    }

    public async Task OpenProjectAsync(string projectId)
    {
        using var connection = _dataService.Database.OpenConnection();
        var project = await _dataService.Projects.FindByIdAsync(projectId, connection);
        if (project is null)
        {
            // 최근 목록에 있던 프로젝트가 DB에서 사라진 경우(예: 다른 경로의 DB 파일) - 조용히 무시하지
            // 않고 호출부가 처리할 수 있게 예외를 던진다. 사용자에게는 ViewModel이 이해 가능한 메시지로
            // 바꿔 보여준다(CLAUDE.md 절대 원칙 4).
            throw new InvalidOperationException($"Project '{projectId}' not found.");
        }

        project.LastOpenedAt = DateTimeOffset.UtcNow;
        await _dataService.Projects.TouchLastOpenedAsync(project.Id, project.LastOpenedAt, connection);

        await SwitchToAsync(project);
    }

    public async Task UpdateProjectAsync(string name, string? client, string? site, string? description)
    {
        if (_currentProject is not { } project)
        {
            return;
        }

        project.Name = name;
        project.Client = client;
        project.Site = site;
        project.Description = description;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        using var connection = _dataService.Database.OpenConnection();
        await _dataService.Projects.UpdateAsync(project, connection);

        OnPropertyChanged(nameof(CurrentProject));
        var existing = FindRecentProject(project.Id);
        if (existing is not null)
        {
            var index = RecentProjects.IndexOf(existing);
            RecentProjects[index] = project;
        }
    }

    public async Task AddQuantityRecordAsync(QuantityRecord record, string activityTitle, string? activityDescription)
    {
        if (_currentProject is not { } project)
        {
            // 빠른 세션 - DB에 남기지 않고 화면에만 보여준다 (§21).
            QuantityRecords.Insert(0, record);
            Activity.Insert(0, new ActivityRecord(
                Guid.NewGuid().ToString("N"), string.Empty, "QuantityAdded", activityTitle, activityDescription, record.CreatedAt));
            return;
        }

        record.ProjectId = project.Id;
        var activity = new ActivityRecord(
            id: Guid.NewGuid().ToString("N"),
            projectId: project.Id,
            activityType: "QuantityAdded",
            title: activityTitle,
            description: activityDescription,
            createdAt: record.CreatedAt);

        await _dataService.AddQuantityRecordWithActivityAsync(record, activity);

        QuantityRecords.Insert(0, record);
        Activity.Insert(0, activity);
    }

    public async Task UpdateQuantityRecordDescriptionAsync(string id, string description)
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var target = FindQuantityRecord(id);
        if (target is not null)
        {
            target.Description = description;
            target.UpdatedAt = updatedAt;
        }

        if (_currentProject is null)
        {
            return;
        }

        using var connection = _dataService.Database.OpenConnection();
        await _dataService.QuantityRecords.UpdateDescriptionAsync(id, description, updatedAt, connection);
    }

    public async Task DeleteQuantityRecordAsync(string id)
    {
        var target = FindQuantityRecord(id);
        if (target is not null)
        {
            QuantityRecords.Remove(target);
        }

        if (_currentProject is null)
        {
            return;
        }

        using var connection = _dataService.Database.OpenConnection();
        await _dataService.QuantityRecords.DeleteAsync(id, connection);
    }

    public async Task RegisterDrawingFileAsync(string fullPath, string fileName, string? drawingUnit)
    {
        if (_currentProject is not { } project || string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var drawingFile = new DrawingFile(
            id: Guid.NewGuid().ToString("N"),
            projectId: project.Id,
            fileName: fileName,
            fullPath: fullPath,
            drawingUnit: drawingUnit,
            lastSeenAt: now,
            lastOpenedAt: now);

        using var connection = _dataService.Database.OpenConnection();
        await _dataService.DrawingFiles.UpsertAsync(drawingFile, connection);
    }

    public async Task AddExportRecordAsync(string? sourceDrawing, string targetFile, int objectCount, string? description)
    {
        if (_currentProject is not { } project)
        {
            return;
        }

        var record = new ExportRecord(
            id: Guid.NewGuid().ToString("N"),
            projectId: project.Id,
            sourceDrawing: sourceDrawing,
            targetFile: targetFile,
            objectCount: objectCount,
            description: description,
            createdAt: DateTimeOffset.UtcNow);

        var activity = new ActivityRecord(
            id: Guid.NewGuid().ToString("N"),
            projectId: project.Id,
            activityType: "ExportCompleted",
            title: "DWG 내보내기 완료",
            description: $"{System.IO.Path.GetFileName(targetFile)} · {objectCount}개 객체",
            createdAt: record.CreatedAt);

        using var connection = _dataService.Database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            await _dataService.ExportRecords.InsertAsync(record, connection, transaction);
            await _dataService.Activity.InsertAsync(activity, connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        Activity.Insert(0, activity);
    }

    public async Task AddExcelExportRecordAsync(string targetFile, int recordCount, string scopeDescription)
    {
        if (_currentProject is not { } project)
        {
            return;
        }

        var record = new ExportRecord(
            id: Guid.NewGuid().ToString("N"),
            projectId: project.Id,
            sourceDrawing: null,
            targetFile: targetFile,
            objectCount: recordCount,
            description: scopeDescription,
            createdAt: DateTimeOffset.UtcNow,
            exportType: ExportTypes.ExcelQuantity);

        var activity = new ActivityRecord(
            id: Guid.NewGuid().ToString("N"),
            projectId: project.Id,
            activityType: "ExcelExportCompleted",
            title: "수량산출서 Excel 저장",
            description: $"{System.IO.Path.GetFileName(targetFile)} · {recordCount}건",
            createdAt: record.CreatedAt);

        using var connection = _dataService.Database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            await _dataService.ExportRecords.InsertAsync(record, connection, transaction);
            await _dataService.Activity.InsertAsync(activity, connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        Activity.Insert(0, activity);
    }

    private async Task SwitchToAsync(Project project)
    {
        CurrentProject = project;

        using var connection = _dataService.Database.OpenConnection();
        var records = await _dataService.QuantityRecords.GetByProjectAsync(project.Id, connection);
        var activity = await _dataService.Activity.GetByProjectAsync(project.Id, ActivityLimit, connection);

        QuantityRecords.Clear();
        // 최신이 위로 오도록 - 저장소는 CreatedAt 오름차순으로 반환한다.
        for (var i = records.Count - 1; i >= 0; i--)
        {
            QuantityRecords.Add(records[i]);
        }

        Activity.Clear();
        foreach (var entry in activity)
        {
            Activity.Add(entry);
        }

        CurrentProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    private QuantityRecord? FindQuantityRecord(string id)
    {
        foreach (var record in QuantityRecords)
        {
            if (record.Id == id)
            {
                return record;
            }
        }

        return null;
    }

    private Project? FindRecentProject(string id)
    {
        foreach (var project in RecentProjects)
        {
            if (project.Id == id)
            {
                return project;
            }
        }

        return null;
    }
}
