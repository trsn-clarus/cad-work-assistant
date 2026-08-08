using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Length;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Parapet;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// "기준 길이 확보 → 높이/면/상부면 입력 → 실시간 계산 → 산출내역 추가" Workflow. VerticalAreaWorkflowViewModel과
/// 같은 구조(§63 "같은 Workflow Infrastructure를 활용한다") - 기준 길이 확보는 똑같이
/// <see cref="LengthSourceSelector"/>에 위임하고, 계산은 ParapetCalculator(내부적으로
/// VerticalAreaCalculator를 두 번 재사용)에 위임한다.
/// </summary>
public sealed class ParapetWorkflowViewModel : ObservableObject
{
    private readonly RelayCommand _addToQuantitySheetCommand;

    private string _heightText = string.Empty;
    private DrawingUnit _heightUnit = DrawingUnit.Meters;
    private ParapetFaceMode _faceMode = ParapetFaceMode.Single;
    private bool _topIncluded;
    private string _topWidthText = string.Empty;
    private DrawingUnit _topWidthUnit = DrawingUnit.Millimeters;
    private ParapetResult? _result;
    private string _statusText = "기준 길이가 필요합니다.";

    public ParapetWorkflowViewModel(IAutoCadConnectionManager connectionManager, LengthWorkflowViewModel lengthWorkflow)
    {
        Source = new LengthSourceSelector(connectionManager, lengthWorkflow);
        Source.PropertyChanged += (_, _) => Recalculate();

        _addToQuantitySheetCommand = new RelayCommand(AddToQuantitySheet, CanAddToQuantitySheet);
        CopyResultCommand = new RelayCommand(CopyResult, () => TotalDisplay is not null);
    }

    public LengthSourceSelector Source { get; }

    public IReadOnlyList<DrawingUnit> HeightUnitOptions => LinearUnitOptions.All;

    public IReadOnlyList<DrawingUnit> TopWidthUnitOptions => LinearUnitOptions.All;

    public ICommand AddToQuantitySheetCommand => _addToQuantitySheetCommand;

    public ICommand CopyResultCommand { get; }

    /// <summary>MainWindowViewModel이 구독해서 자신의 QuantityRecords에 붙인다 - 소유권을 갖지 않는다.</summary>
    public event System.EventHandler<QuantityRecord>? RecordAdded;

    public string HeightText
    {
        get => _heightText;
        set
        {
            if (SetProperty(ref _heightText, value))
            {
                Recalculate();
            }
        }
    }

    public DrawingUnit HeightUnit
    {
        get => _heightUnit;
        set
        {
            if (SetProperty(ref _heightUnit, value))
            {
                Recalculate();
            }
        }
    }

    public ParapetFaceMode FaceMode
    {
        get => _faceMode;
        set
        {
            if (SetProperty(ref _faceMode, value))
            {
                Recalculate();
            }
        }
    }

    public bool IsSingleFace
    {
        get => FaceMode == ParapetFaceMode.Single;
        set { if (value) FaceMode = ParapetFaceMode.Single; }
    }

    public bool IsBothFaces
    {
        get => FaceMode == ParapetFaceMode.Both;
        set { if (value) FaceMode = ParapetFaceMode.Both; }
    }

    public bool TopIncluded
    {
        get => _topIncluded;
        set
        {
            if (SetProperty(ref _topIncluded, value))
            {
                Recalculate();
            }
        }
    }

    public string TopWidthText
    {
        get => _topWidthText;
        set
        {
            if (SetProperty(ref _topWidthText, value))
            {
                Recalculate();
            }
        }
    }

    public DrawingUnit TopWidthUnit
    {
        get => _topWidthUnit;
        set
        {
            if (SetProperty(ref _topWidthUnit, value))
            {
                Recalculate();
            }
        }
    }

    public bool IsReady => _result is not null;

    public bool IsInvalidInput => !Source.IsBusy && Source.HasLength && _result is null;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public Brush StatusBrush => (Brush?)Application.Current?.TryFindResource(StatusBrushKey) ?? Brushes.Gray;

    private string StatusBrushKey => Source.IsBusy
        ? "BrushWarning"
        : Source.IsError
            ? "BrushError"
            : _result is not null
                ? "BrushSuccess"
                : (IsInvalidInput ? "BrushWarning" : "BrushTextMuted");

    /// <summary>측면 총 면적 - Both면 이미 x2 적용된 값 (§27).</summary>
    public string? SideAreaDisplay => _result is { } result
        ? AreaFormatter.FormatSquareMetersWithUnit(result.SideAreaSquareMeters, decimalPlaces: 3)
        : null;

    public string? SideFormulaDisplay => _result is { } result
        ? (result.FaceMultiplier == 1
            ? $"{LengthFormatter.FormatMeters(result.SourceLengthMeters)} m × {LengthFormatter.FormatMeters(result.HeightMeters)} m"
            : $"{LengthFormatter.FormatMeters(result.SourceLengthMeters)} m × {LengthFormatter.FormatMeters(result.HeightMeters)} m × {result.FaceMultiplier}")
        : null;

    public string? TopAreaDisplay => _result is { TopIncluded: true } result
        ? AreaFormatter.FormatSquareMetersWithUnit(result.TopAreaSquareMeters, decimalPlaces: 3)
        : null;

    public string? TopFormulaDisplay => _result is { TopIncluded: true } result
        ? $"{LengthFormatter.FormatMeters(result.SourceLengthMeters)} m × {LengthFormatter.FormatMeters(result.TopWidthMeters)} m"
        : null;

    public string? TotalDisplay => _result is { } result
        ? AreaFormatter.FormatSquareMetersWithUnit(result.TotalAreaSquareMeters, decimalPlaces: 3)
        : null;

    private bool CanAddToQuantitySheet() => _result is not null;

    private void Recalculate()
    {
        if (Source.IsBusy)
        {
            _result = null;
            StatusText = Source.StatusText ?? "AutoCAD에서 기준선 선택 대기 중...";
            NotifyResultChanged();
            return;
        }

        if (!Source.HasLength)
        {
            _result = null;
            StatusText = Source.StatusText ?? "기준 길이가 필요합니다.";
            NotifyResultChanged();
            return;
        }

        if (!TryParsePositive(HeightText, out var heightRaw, requiredMessage: "높이를 입력하세요.", out var heightError))
        {
            _result = null;
            StatusText = heightError!;
            NotifyResultChanged();
            return;
        }

        double topWidthRaw = 0;
        if (TopIncluded && !TryParsePositive(TopWidthText, out topWidthRaw, requiredMessage: "상부 폭을 입력하세요.", out var widthError))
        {
            _result = null;
            StatusText = widthError!;
            NotifyResultChanged();
            return;
        }

        var validation = ParapetCalculator.Validate(heightRaw, TopIncluded, topWidthRaw);
        if (!validation.IsValid)
        {
            _result = null;
            StatusText = !validation.IsHeightValid ? "높이는 0보다 커야 합니다." : "상부 폭은 0보다 커야 합니다.";
            NotifyResultChanged();
            return;
        }

        var input = new ParapetInput(Source.LengthMeters!.Value, heightRaw, HeightUnit, FaceMode, TopIncluded, topWidthRaw, TopWidthUnit);
        _result = ParapetCalculator.Calculate(input, System.DateTimeOffset.Now);
        StatusText = "파라펫 면적을 계산했습니다.";
        NotifyResultChanged();
    }

    private static bool TryParsePositive(string text, out double value, string requiredMessage, out string? errorMessage)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            errorMessage = requiredMessage;
            return false;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            errorMessage = "유효한 숫자를 입력해주세요.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private void NotifyResultChanged()
    {
        OnPropertyChanged(nameof(SideAreaDisplay));
        OnPropertyChanged(nameof(SideFormulaDisplay));
        OnPropertyChanged(nameof(TopAreaDisplay));
        OnPropertyChanged(nameof(TopFormulaDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsInvalidInput));
        OnPropertyChanged(nameof(StatusBrush));
        _addToQuantitySheetCommand.RaiseCanExecuteChanged();
        ((RelayCommand)CopyResultCommand).RaiseCanExecuteChanged();
    }

    private void AddToQuantitySheet()
    {
        if (_result is not { } result)
        {
            return;
        }

        var expression = result.TopIncluded
            ? $"측면: {SideFormulaDisplay} = {SideAreaDisplay}\n상부: {TopFormulaDisplay} = {TopAreaDisplay}\n합계: {TotalDisplay}"
            : $"{SideFormulaDisplay} = {TotalDisplay}";

        var record = new QuantityRecord(
            id: "Q-" + System.DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            type: "Parapet",
            layer: Source.LayerDisplay,
            objectCount: Source.ObjectHandles.Count,
            value: (decimal)result.TotalAreaSquareMeters,
            unit: "m²",
            sourceDrawing: Source.DrawingName ?? "Manual Entry",
            createdAt: result.CreatedAt,
            rawValue: (decimal)result.SourceLengthMeters,
            sourceUnit: "m",
            objectHandles: Source.ObjectHandles,
            calculationExpression: expression,
            measurementSource: Source.SourceType.ToString());

        RecordAdded?.Invoke(this, record);
        StatusText = "산출내역에 추가했습니다.";
    }

    private void CopyResult()
    {
        if (TotalDisplay is not { } text)
        {
            return;
        }

        Clipboard.SetText(text);
        StatusText = "결과를 클립보드에 복사했습니다.";
    }
}
