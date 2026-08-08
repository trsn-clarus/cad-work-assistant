using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CADWorkAssistant.Desktop.Converters;

/// <summary>
/// 값 텍스트가 null/빈 문자열이면 단위 라벨도 함께 숨긴다 - 결과가 없는데 "m²"만 덩그러니 떠 있는
/// 것을 막는다 (Milestone 4.5 §16-17, 숫자+단위를 별도 TextBlock으로 나누면서 필요해졌다).
/// </summary>
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
