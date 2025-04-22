using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FilterDataGrid
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool Inverse { get; set; }
        public bool Collapse { get; set; } = true;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = (value is bool b && b);
            if (Inverse)
                flag = !flag;

            if (flag)
                return Visibility.Visible;
            return Collapse ? Visibility.Collapsed : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool result = visibility == Visibility.Visible;
                return Inverse ? !result : result;
            }
            return false;
        }
    }
}
