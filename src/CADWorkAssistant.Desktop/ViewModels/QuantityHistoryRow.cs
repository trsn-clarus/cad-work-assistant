using System;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Desktop.Common;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// Quantity History 테이블의 행 하나 - QuantityRecord + 최신 검산 결과 + 최신 검토 상태를 한 곳에
/// 묶는다(§62 Inspector RESULT/VERIFICATION/REVIEW 섹션이 전부 이 한 객체에서 나온다). 색상만으로
/// 상태를 전달하지 않는다 - Glyph+Label을 항상 같이 노출한다(§58, §113).
/// </summary>
public sealed class QuantityHistoryRow : ObservableObject
{
    private bool _isChecked;
    private QuantityVerificationResult? _verification;
    private QuantityReview? _review;

    public QuantityHistoryRow(QuantityRecord record)
    {
        Record = record;
    }

    public QuantityRecord Record { get; }

    public string Id => Record.Id;

    public string Type => Record.Type;

    public string Layer => Record.Layer;

    public string Description => Record.Description;

    public decimal Value => Record.Value;

    public string Unit => Record.Unit;

    public string SourceDrawing => Record.SourceDrawing;

    public DateTimeOffset CreatedAt => Record.CreatedAt;

    /// <summary>Batch 검산/비교 대상 선택 - Inspector를 여는 단일 선택(SelectedRow)과는 별개다(§116,
    /// WPF DataGrid의 멀티 선택 바인딩이 번거로워 체크박스 열로 대신했다).</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    public QuantityVerificationResult? Verification
    {
        get => _verification;
        set
        {
            if (SetProperty(ref _verification, value))
            {
                OnPropertyChanged(nameof(HasVerification));
                OnPropertyChanged(nameof(VerificationGlyph));
                OnPropertyChanged(nameof(VerificationLabel));
                OnPropertyChanged(nameof(IsStale));
            }
        }
    }

    public QuantityReview? Review
    {
        get => _review;
        set
        {
            if (SetProperty(ref _review, value))
            {
                OnPropertyChanged(nameof(ReviewStatus));
                OnPropertyChanged(nameof(ReviewLabel));
                OnPropertyChanged(nameof(ReviewNote));
            }
        }
    }

    /// <summary>XAML에서 InverseBooleanToVisibilityConverter는 진짜 bool만 받는다 - nullable 참조형
    /// (Verification)을 직접 바인딩하면 "값이 있어도 항상 true가 아니므로 항상 Visible"이 되는 실제
    /// 렌더링 버그가 났다(Simulation Mode 스크린샷으로 발견) - 이 bool 프로퍼티를 대신 바인딩한다.</summary>
    public bool HasVerification => Verification is not null;

    /// <summary>§58: ✓ 검산 완료 / ! 확인 필요 / × 오류 / — 미검산 / ? 검산 불가(Info - 기계 검증은
    /// 안 됐지만 확실한 문제도 아님). Excel Export도 같은 문구를 써야 해서 Core로 옮긴 정책을
    /// 그대로 위임한다(Milestone 9, Core.Verification.VerificationSeverityDisplay).</summary>
    public string VerificationGlyph => VerificationSeverityDisplay.Glyph(Verification?.OverallSeverity);

    public string VerificationLabel => VerificationSeverityDisplay.Label(Verification?.OverallSeverity);

    /// <summary>저장된 Snapshot이 지금 엔진의 RuleSetVersion보다 낮은 규칙으로 만들어졌다 - "재검산
    /// 필요" 상태(§50).</summary>
    public bool IsStale => Verification is not null
        && Verification.RuleSetVersion < QuantityVerificationService.CurrentRuleSetVersion;

    public QuantityReviewStatus ReviewStatus => Review?.Status ?? QuantityReviewStatus.Unreviewed;

    public string ReviewLabel => QuantityReviewStatusDisplay.Label(ReviewStatus);

    public string? ReviewNote => Review?.Note;
}
