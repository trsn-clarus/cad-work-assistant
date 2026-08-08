using System;
using System.Globalization;
using System.Windows.Data;
using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Desktop.Converters;

/// <summary>개별 Verification Check 하나의 Severity를 기호로 바꾼다 - Quantity History Inspector의
/// Check 목록(§63)에서 쓴다. Row 전체 상태(QuantityHistoryRow.VerificationGlyph, 5단계)와 달리
/// 이건 Check 하나짜리라 Pass/Info/Review/Error 4단계만 있다.</summary>
public sealed class VerificationSeverityToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        VerificationSeverity.Pass => "✓",
        VerificationSeverity.Info => "ⓘ",
        VerificationSeverity.Review => "!",
        VerificationSeverity.Error => "×",
        _ => "?"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
