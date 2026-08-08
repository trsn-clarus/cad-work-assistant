using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// "새 프로젝트" 생성 폼 + "최근 프로젝트" 목록을 한 창에서 다룬다 (Milestone 6 §31-36) - 별도
/// 페이지로 쪼개지 않는다(§18의 "초기 구현에서 너무 많은 페이지로 쪼개지 않아도 된다"는 원칙을
/// Project UI에도 그대로 적용). 성공적으로 만들거나 연 뒤에는 <see cref="RequestClose"/>를 올려
/// Window가 스스로 닫게 한다.
/// </summary>
public sealed class ProjectDialogViewModel : ObservableObject
{
    private readonly IProjectContextService _projectContext;

    private string _newProjectName = string.Empty;
    private string _newProjectClient = string.Empty;
    private string _newProjectSite = string.Empty;
    private string _newProjectDescription = string.Empty;
    private string? _statusText;
    private bool _isError;
    private bool _isBusy;

    public ProjectDialogViewModel(IProjectContextService projectContext)
    {
        _projectContext = projectContext;
        _projectContext.RecentProjects.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecentProjects));
        CreateProjectCommand = new RelayCommand(CreateProject, () => !_isBusy && !string.IsNullOrWhiteSpace(_newProjectName));
        OpenProjectCommand = new RelayCommand(OpenProject, _ => !_isBusy);
    }

    public ObservableCollection<Project> RecentProjects => _projectContext.RecentProjects;

    public bool HasRecentProjects => RecentProjects.Count > 0;

    public string NewProjectName
    {
        get => _newProjectName;
        set
        {
            if (SetProperty(ref _newProjectName, value))
            {
                ((RelayCommand)CreateProjectCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string NewProjectClient
    {
        get => _newProjectClient;
        set => SetProperty(ref _newProjectClient, value);
    }

    public string NewProjectSite
    {
        get => _newProjectSite;
        set => SetProperty(ref _newProjectSite, value);
    }

    public string NewProjectDescription
    {
        get => _newProjectDescription;
        set => SetProperty(ref _newProjectDescription, value);
    }

    public string? StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public ICommand CreateProjectCommand { get; }

    public ICommand OpenProjectCommand { get; }

    /// <summary>Window가 자기 자신을 닫기 위해 구독한다 - ViewModel은 Window를 직접 참조하지 않는다.</summary>
    public event EventHandler? RequestClose;

    private async void CreateProject()
    {
        _isBusy = true;
        IsError = false;
        StatusText = "프로젝트를 만드는 중...";

        try
        {
            await _projectContext.CreateProjectAsync(
                _newProjectName.Trim(),
                string.IsNullOrWhiteSpace(_newProjectClient) ? null : _newProjectClient.Trim(),
                string.IsNullOrWhiteSpace(_newProjectSite) ? null : _newProjectSite.Trim(),
                string.IsNullOrWhiteSpace(_newProjectDescription) ? null : _newProjectDescription.Trim());

            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CreateProjectAsync failed");
            StatusText = "프로젝트를 만들지 못했습니다.\n\n잠시 후 다시 시도해주세요.";
            IsError = true;
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async void OpenProject(object? parameter)
    {
        if (parameter is not Project project)
        {
            return;
        }

        _isBusy = true;
        IsError = false;
        StatusText = $"'{project.Name}' 여는 중...";

        try
        {
            await _projectContext.OpenProjectAsync(project.Id);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenProjectAsync failed for {ProjectId}", project.Id);
            StatusText = "프로젝트를 열지 못했습니다.\n\n잠시 후 다시 시도해주세요.";
            IsError = true;
        }
        finally
        {
            _isBusy = false;
        }
    }
}
