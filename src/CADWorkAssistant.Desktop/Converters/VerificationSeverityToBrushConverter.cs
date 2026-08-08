using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Desktop.Converters;

/// <summary>Glyph/Label과 항상 같이 쓴다 - 색상만으로 상태를 전달하지 않기 위해서다(§113). Existing
/// Connection Status 브러시(BrushSuccess/BrushWarning/BrushError)를 재사용한다 - 이 화면만을 위한
/// 새 색상을 만들지 않는다.</summary>
public sealed class VerificationSeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            VerificationSeverity.Pass => "BrushSuccess",
            VerificationSeverity.Review => "BrushWarning",
            VerificationSeverity.Error => "BrushError",
            _ => "BrushTextMuted"
        };

        return (Brush?)Application.Current?.TryFindResource(key) ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
