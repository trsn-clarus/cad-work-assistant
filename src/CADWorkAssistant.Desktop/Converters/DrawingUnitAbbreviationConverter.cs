using System;
using System.Globalization;
using System.Windows.Data;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Desktop.Converters;

/// <summary>ComboBox에 DrawingUnit.Millimeters 대신 "mm"을 보여준다 - Core.Cad.DrawingUnitDisplay를 그대로 재사용한다.</summary>
public sealed class DrawingUnitAbbreviationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DrawingUnit unit ? DrawingUnitDisplay.Abbreviation(unit) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
