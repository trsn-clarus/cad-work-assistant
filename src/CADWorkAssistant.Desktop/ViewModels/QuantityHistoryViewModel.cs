using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// Quantity History 화면 전체 - 저장된 산출내역 목록 + 검산/검토 상태 + Inspector + 배치 검산 +
/// 비교(§55-74). Sheet(Dashboard의 Quantity Results)는 "지금 프로젝트의 공식 산출내역"을 보여주고,
/// History는 "그 기록의 검산·비교·추적"을 다룬다(§56) - 서로 역할이 다르므로 통합하지 않았다.
/// </summary>
public sealed class QuantityHistoryViewModel : ObservableObject
{
    private const string AllFilter = "전체";

    private readonly IProjectContextService _projectContext;
    private readonly IQuantityVerificationCoordinator _coordinator;
    private readonly RelayCommand _verifySelectedCommand;
    private readonly RelayCommand _verifyAllCommand;
    private readonly RelayCommand _cancelVerificationCommand;
    private readonly RelayCommand _markVerifiedCommand;
    private readonly RelayCommand _markNeedsReviewCommand;
    private readonly RelayCommand _saveReviewNoteCommand;
    private readonly RelayCommand _compareCommand;

    private CancellationTokenSource? _verificationCts;
    private string _searchText = string.Empty;
    private string _typeFilter = AllFilter;
    private string _verificationFilter = AllFilter;
    private string _reviewFilter = AllFilter;
    private QuantityHistoryRow? _selectedRow;
    private bool _isVerifying;
    private string? _batchProgressText;
    private string? _statusMessage;
    private string _reviewNoteDraft = string.Empty;
    private string? _compareResultText;

    public QuantityHistoryViewModel(IProjectContextService projectContext, IQuantityVerificationCoordinator coordinator)
    {
        _projectContext = projectContext;
        _coordinator = coordinator;

        Rows = new ObservableCollection<QuantityHistoryRow>();
        FilteredRows = new ObservableCollection<QuantityHistoryRow>();

        _projectContext.QuantityRecords.CollectionChanged += (_, _) => RebuildRows();
        _projectContext.CurrentProjectChanged += async (_, _) => await RefreshAsync();

        _verifySelectedCommand = new RelayCommand(VerifySelected, () => !IsVerifying && Rows.Any(r => r.IsChecked));
        _verifyAllCommand = new RelayCommand(VerifyAll, () => !IsVerifying && Rows.Count > 0);
        _cancelVerificationCommand = new RelayCommand(CancelVerification, () => IsVerifying);
        _markVerifiedCommand = new RelayCommand(() => SaveReview(QuantityReviewStatus.Verified), () => SelectedRow is not null);
        _markNeedsReviewCommand = new RelayCommand(() => SaveReview(QuantityReviewStatus.NeedsReview), () => SelectedRow is not null);
        _saveReviewNoteCommand = new RelayCommand(SaveReviewNote, () => SelectedRow is not null);
        _compareCommand = new RelayCommand(Compare, CanCompare);

        RebuildRows();
    }

    public ObservableCollection<QuantityHistoryRow> Rows { get; }

    public ObservableCollection<QuantityHistoryRow> FilteredRows { get; }

    public IReadOnlyList<string> TypeFilterOptions { get; } = new[] { AllFilter, "Length", "Area", "VerticalArea", "Parapet" };

    public IReadOnlyList<string> VerificationFilterOptions { get; } = new[] { AllFilter, "검산 완료", "확인 필요", "오류", "미검산", "검산 불가" };

    public IReadOnlyList<string> ReviewFilterOptions { get; } = new[] { AllFilter, "미검토", "검토 완료", "확인 필요" };

    public ICommand VerifySelectedCommand => _verifySelectedCommand;

    public ICommand VerifyAllCommand => _verifyAllCommand;

    public ICommand CancelVerificationCommand => _cancelVerificationCommand;

    public ICommand MarkVerifiedCommand => _markVerifiedCommand;

    public ICommand MarkNeedsReviewCommand => _markNeedsReviewCommand;

    public ICommand SaveReviewNoteCommand => _saveReviewNoteCommand;

    public ICommand CompareCommand => _compareCommand;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string TypeFilter
    {
        get => _typeFilter;
        set
        {
            if (SetProperty(ref _typeFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string VerificationFilter
    {
        get => _verificationFilter;
        set
        {
            if (SetProperty(ref _verificationFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string ReviewFilter
    {
        get => _reviewFilter;
        set
        {
            if (SetProperty(ref _reviewFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public QuantityHistoryRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                ReviewNoteDraft = value?.ReviewNote ?? string.Empty;
                OnPropertyChanged(nameof(HasSelection));
                _markVerifiedCommand.RaiseCanExecuteChanged();
                _markNeedsReviewCommand.RaiseCanExecuteChanged();
                _saveReviewNoteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedRow is not null;

    public string ReviewNoteDraft
    {
        get => _reviewNoteDraft;
        set => SetProperty(ref _reviewNoteDraft, value);
    }

    public bool IsVerifying
    {
        get => _isVerifying;
        private set
        {
            if (SetProperty(ref _isVerifying, value))
            {
                _verifySelectedCommand.RaiseCanExecuteChanged();
                _verifyAllCommand.RaiseCanExecuteChanged();
                _cancelVerificationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? BatchProgressText
    {
        get => _batchProgressText;
        private set => SetProperty(ref _batchProgressText, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? CompareResultText
    {
        get => _compareResultText;
        private set => SetProperty(ref _compareResultText, value);
    }

    public bool HasCompareResult => CompareResultText is not null;

    /// <summary>§70: "42건 검산 · 통과 35 · 확인 필요 5 · 오류 1 · 검산 불가 1" - 거대한 KPI 카드
    /// 대신 한 줄 요약.</summary>
    public string SummaryText
    {
        get
        {
            var total = Rows.Count;
            var passed = Rows.Count(r => r.Verification?.OverallSeverity == VerificationSeverity.Pass);
            var review = Rows.Count(r => r.Verification?.OverallSeverity == VerificationSeverity.Review);
            var error = Rows.Count(r => r.Verification?.OverallSeverity == VerificationSeverity.Error);
            var notChecked = Rows.Count(r => r.Verification is null);
            return $"{total}건 · 검산 완료 {passed} · 확인 필요 {review} · 오류 {error} · 미검산 {notChecked}";
        }
    }

    /// <summary>MainWindowViewModel이 Navigation에서 History로 전환할 때 호출한다(Drawing.OnActivated와
    /// 같은 패턴) - 매번 다시 검산하지 않는다, 저장된 최신 상태만 다시 불러온다(§49).</summary>
    public async void OnActivated()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            RebuildRows();
            var snapshotSet = await _coordinator.LoadForProjectAsync(_projectContext.CurrentProject?.Id);
            ApplySnapshotSet(snapshotSet);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "QuantityHistory refresh failed");
            StatusMessage = "검산/검토 기록을 불러오지 못했습니다.";
        }
    }

    private void RebuildRows()
    {
        var checkedIds = Rows.Where(r => r.IsChecked).Select(r => r.Id).ToHashSet();
        var existingByRecordId = Rows.ToDictionary(r => r.Id);

        Rows.Clear();
        foreach (var record in _projectContext.QuantityRecords)
        {
            var row = new QuantityHistoryRow(record) { IsChecked = checkedIds.Contains(record.Id) };
            if (existingByRecordId.TryGetValue(record.Id, out var existing))
            {
                row.Verification = existing.Verification;
                row.Review = existing.Review;
            }

            // 체크박스는 DataGrid 셀 편집이라 WPF의 CommandManager.RequerySuggested 자동 재조회
            // 타이밍에 기대지 않는다 - IsChecked가 바뀔 때마다 선택 항목 검산/비교 버튼의
            // CanExecute를 명시적으로 다시 묻는다(UI Automation으로 체크박스를 토글해 실제로
            // 검증했을 때 자동 재조회가 안 미더운 것을 확인했다).
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(QuantityHistoryRow.IsChecked))
                {
                    _verifySelectedCommand.RaiseCanExecuteChanged();
                    _compareCommand.RaiseCanExecuteChanged();
                }
            };

            Rows.Add(row);
        }

        ApplyFilter();
        OnPropertyChanged(nameof(SummaryText));
    }

    private void ApplySnapshotSet(QuantityHistorySnapshotSet snapshotSet)
    {
        foreach (var row in Rows)
        {
            row.Verification = snapshotSet.Verifications.TryGetValue(row.Id, out var verification) ? verification : null;
            row.Review = snapshotSet.Reviews.TryGetValue(row.Id, out var review) ? review : null;
        }

        ApplyFilter();
        OnPropertyChanged(nameof(SummaryText));
    }

    private void ApplyFilter()
    {
        FilteredRows.Clear();

        foreach (var row in Rows)
        {
            if (TypeFilter != AllFilter && row.Type != TypeFilter)
            {
                continue;
            }

            if (VerificationFilter != AllFilter && row.VerificationLabel != VerificationFilter)
            {
                continue;
            }

            if (ReviewFilter != AllFilter && row.ReviewLabel != ReviewFilter)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(SearchText)
                && row.Description.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) < 0
                && row.SourceDrawing.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            FilteredRows.Add(row);
        }
    }

    private async void VerifySelected()
    {
        var targets = Rows.Where(r => r.IsChecked).ToList();
        await RunBatchAsync(targets);
    }

    private async void VerifyAll()
    {
        await RunBatchAsync(Rows.ToList());
    }

    private async Task RunBatchAsync(List<QuantityHistoryRow> targets)
    {
        if (targets.Count == 0 || IsVerifying)
        {
            return;
        }

        _verificationCts = new CancellationTokenSource();
        IsVerifying = true;
        BatchProgressText = $"검산 중... 0 / {targets.Count}";

        try
        {
            var allRecords = _projectContext.QuantityRecords.ToList();
            var targetRecords = targets.Select(r => r.Record).ToList();
            var progress = new Progress<QuantityVerificationBatchProgress>(p =>
                BatchProgressText = $"검산 중... {p.Completed} / {p.Total}");

            var summary = await _coordinator.VerifyBatchAsync(targetRecords, allRecords, progress, _verificationCts.Token);

            foreach (var row in targets)
            {
                if (summary.Results.TryGetValue(row.Id, out var result))
                {
                    row.Verification = result;
                }
            }

            StatusMessage = summary.Failed > 0
                ? $"{summary.Total}건 중 {summary.Failed}건 검산에 실패했습니다."
                : $"{summary.Total}건을 검산했습니다.";
            OnPropertyChanged(nameof(SummaryText));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "검산이 취소되었습니다.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Batch verification failed");
            StatusMessage = "검산 중 오류가 발생했습니다.";
        }
        finally
        {
            IsVerifying = false;
            BatchProgressText = null;
            _verificationCts?.Dispose();
            _verificationCts = null;
        }
    }

    private void CancelVerification()
    {
        _verificationCts?.Cancel();
    }

    private async void SaveReview(QuantityReviewStatus status)
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        try
        {
            // 화면에 지금 입력되어 있는 메모(ReviewNoteDraft)를 함께 저장한다 - row.ReviewNote(마지막
            // 저장본)를 쓰면 "메모를 적고 바로 검토 완료를 누르는" 자연스러운 흐름에서 방금 입력한
            // 메모가 조용히 사라지는 실제 버그가 있었다(Simulation Mode 재현으로 발견).
            var review = await _coordinator.SaveReviewAsync(row.Record, status, ReviewNoteDraft, logActivity: true);
            row.Review = review;
            StatusMessage = status == QuantityReviewStatus.Verified ? "검토 완료로 표시했습니다." : "확인 필요로 표시했습니다.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SaveReview failed");
            StatusMessage = "검토 상태를 저장하지 못했습니다.";
        }
    }

    private async void SaveReviewNote()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        try
        {
            var status = row.ReviewStatus;
            var review = await _coordinator.SaveReviewAsync(row.Record, status, ReviewNoteDraft, logActivity: false);
            row.Review = review;
            StatusMessage = "메모를 저장했습니다.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SaveReviewNote failed");
            StatusMessage = "메모를 저장하지 못했습니다.";
        }
    }

    private bool CanCompare() => Rows.Count(r => r.IsChecked) == 2
        && Rows.Where(r => r.IsChecked).Select(r => r.Unit).Distinct().Count() == 1;

    /// <summary>§71-74: 호환되는 두 기록만 비교하고, 저장은 하지 않는다(읽기 전용 분석). "Revision
    /// 1/2" 같은 이름을 붙이지 않는다(§75) - 사용자가 지정하지 않은 설계 순서를 함부로 추측하지 않고
    /// CreatedAt으로만 "이전/현재"를 구분한다.</summary>
    private void Compare()
    {
        var selected = Rows.Where(r => r.IsChecked).OrderBy(r => r.CreatedAt).ToList();
        if (selected.Count != 2)
        {
            return;
        }

        var previous = selected[0];
        var current = selected[1];
        var difference = current.Value - previous.Value;
        var percent = previous.Value == 0 ? (decimal?)null : difference / previous.Value * 100m;
        var direction = difference >= 0 ? "+" : string.Empty;

        CompareResultText =
            $"이전 ({previous.CreatedAt:yyyy-MM-dd}, {previous.SourceDrawing}): {previous.Value:0.###} {previous.Unit}\n" +
            $"현재 ({current.CreatedAt:yyyy-MM-dd}, {current.SourceDrawing}): {current.Value:0.###} {current.Unit}\n" +
            $"차이: {direction}{difference:0.###} {current.Unit}" +
            (percent is { } p ? $" ({direction}{p:0.#}%)" : string.Empty);
        OnPropertyChanged(nameof(HasCompareResult));
    }
}
