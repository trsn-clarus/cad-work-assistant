using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Length;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.VerticalArea;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// "기준 길이 확보 → 높이 입력 → 실시간 계산 → 산출내역 추가" Workflow (Milestone 4 §20). 기준 길이
/// 확보(CAD 선택/최근 측정값/수동 입력)는 <see cref="LengthSourceSelector"/>에 위임한다 - 새 AutoCAD
/// Selection Handler를 만들지 않는다, 기준 길이는 항상 Milestone 2의 SelectLengthObjects를 통해서만
/// 들어온다(§5-6).
/// </summary>
public sealed class VerticalAreaWorkflowViewModel : ObservableObject
{
    private readonly RelayCommand _addToQuantitySheetCommand;

    private string _heightText = string.Empty;
    private DrawingUnit _heightUnit = DrawingUnit.Meters;
    private VerticalAreaResult? _result;
    private string _statusText = "기준 길이가 필요합니다.";

    public VerticalAreaWorkflowViewModel(IAutoCadConnectionManager connectionManager, LengthWorkflowViewModel lengthWorkflow)
    {
        Source = new LengthSourceSelector(connectionManager, lengthWorkflow);
        Source.PropertyChanged += (_, _) => Recalculate();

        _addToQuantitySheetCommand = new RelayCommand(AddToQuantitySheet, CanAddToQuantitySheet);
        CopyResultCommand = new RelayCommand(CopyResult, () => TotalDisplay is not null);
    }

    public LengthSourceSelector Source { get; }

    public IReadOnlyList<DrawingUnit> HeightUnitOptions => LinearUnitOptions.All;

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

    public bool IsReady => _result is not null;

    /// <summary>길이는 있는데 높이가 아직 무효한 상태 - 색만으로 전달하지 않기 위한 배지에 쓴다 (§65, §89).</summary>
    public bool IsInvalidHeight => !Source.IsBusy && Source.HasLength && _result is null;

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
                : (IsInvalidHeight ? "BrushWarning" : "BrushTextMuted");

    public string? TotalDisplay => _result is { } result
        ? AreaFormatter.FormatSquareMetersWithUnit(result.AreaSquareMeters, decimalPlaces: 3)
        : null;

    /// <summary>"255.941 m × 0.100 m" - 산식을 사용자가 확인할 수 있게 한다 (§76-77).</summary>
    public string? FormulaDisplay => _result is { } result
        ? $"{LengthFormatter.FormatMeters(result.SourceLengthMeters)} m × {LengthFormatter.FormatMeters(result.HeightMeters)} m"
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

        if (string.IsNullOrWhiteSpace(HeightText))
        {
            _result = null;
            StatusText = "높이를 입력하세요.";
            NotifyResultChanged();
            return;
        }

        if (!double.TryParse(HeightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var heightRaw))
        {
            _result = null;
            StatusText = "유효한 숫자를 입력해주세요.";
            NotifyResultChanged();
            return;
        }

        if (VerticalAreaCalculator.Validate(heightRaw) != VerticalAreaValidation.Valid)
        {
            _result = null;
            StatusText = "높이는 0보다 커야 합니다.";
            NotifyResultChanged();
            return;
        }

        _result = VerticalAreaCalculator.Calculate(Source.LengthMeters!.Value, heightRaw, HeightUnit, System.DateTimeOffset.Now);
        StatusText = "수직면적을 계산했습니다.";
        NotifyResultChanged();
    }

    private void NotifyResultChanged()
    {
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(FormulaDisplay));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsInvalidHeight));
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

        var expression = $"{FormulaDisplay} = {TotalDisplay}";

        var record = new QuantityRecord(
            id: "Q-" + System.DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            type: "VerticalArea",
            layer: Source.LayerDisplay,
            objectCount: Source.ObjectHandles.Count,
            value: (decimal)result.AreaSquareMeters,
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
