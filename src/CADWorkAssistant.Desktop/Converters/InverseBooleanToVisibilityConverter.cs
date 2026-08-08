using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CADWorkAssistant.Desktop.Converters;

/// <summary>
/// Empty State 문구는 컬렉션이 비어 있을 때만 보여야 한다 - BoolToVisibility의 반대 방향이 필요해서
/// 별도로 둔다 (Milestone 4.5 §55-57, Dashboard의 QuantityRecords/Activity Empty State).
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
