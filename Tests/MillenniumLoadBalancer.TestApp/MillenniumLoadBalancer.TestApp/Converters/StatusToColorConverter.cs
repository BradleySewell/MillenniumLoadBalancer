using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MillenniumLoadBalancer.TestApp.ViewModels;

namespace MillenniumLoadBalancer.TestApp
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    RequestStatus.Pending => Brushes.Gray,
                    RequestStatus.Sending => Brushes.Orange,
                    RequestStatus.Success => Brushes.Green,
                    RequestStatus.Failed => Brushes.Red,
                    _ => Brushes.Black
                };
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
