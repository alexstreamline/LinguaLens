using System.Globalization;
using System.Windows.Data;

namespace LinguaLens.App.Converters;

/// <summary>
/// Эмулирует CSS letter-spacing для коротких UPPERCASE-заголовков. WPF не умеет
/// разрядку нативно, поэтому вставляем тонкий пробел U+2009 между символами.
/// Использование: Text="{Binding Source=ОРИГИНАЛ, Converter={StaticResource LetterSpacedConverter}}".
/// </summary>
public sealed class LetterSpacedConverter : IValueConverter
{
    public string Spacer { get; set; } = " "; // thin space

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string s || s.Length <= 1) return value;
        return string.Join(Spacer, s.ToCharArray().Select(c => c.ToString()));
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
