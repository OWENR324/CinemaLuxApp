using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyWpfApp
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Проверяем, если строка не пустая, то возвращаем Visibility.Visible, иначе - Visibility.Collapsed
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Не нужно реализовывать, если обратное преобразование не требуется
            throw new NotImplementedException();
        }
    }
}
