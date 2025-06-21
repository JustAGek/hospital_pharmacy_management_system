using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfApp1
{
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal dec)
                return dec.ToString("N2") + " EGP"; 
            if (value is double dbl)
                return dbl.ToString("N2") + " EGP";
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
