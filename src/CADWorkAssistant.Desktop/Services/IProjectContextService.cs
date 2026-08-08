using System;
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

    Task UpdateProjectAsync(string name, string? client, string? site, string? description);

    /// <summary>QuantityRecord 저장 + 그에 대응하는 ActivityRecord 기록을 하나의 단위로 처리한다
    /// (§45-46). Project가 열려 있으면 DB에 트랜잭션으로 남고, 아니면 메모리에만 반영된다.</summary>
    Task AddQuantityRecordAsync(QuantityRecord record, string activityTitle, string? activityDescription);

    Task UpdateQuantityRecordDescriptionAsync(string id, string description);

    Task DeleteQuantityRecordAsync(string id);

    /// <summary>AutoCAD 연결이 도면을 열 때마다 자동으로 호출된다 (§34-37) - 파일을 복사하지 않고
    /// 경로만 참조로 남긴다. 같은 경로면 새 행 대신 갱신된다.</summary>
    Task RegisterDrawingFileAsync(string fullPath, string fileName, string? drawingUnit);

    Task AddExportRecordAsync(string? sourceDrawing, string targetFile, int objectCount, string? description);
}
