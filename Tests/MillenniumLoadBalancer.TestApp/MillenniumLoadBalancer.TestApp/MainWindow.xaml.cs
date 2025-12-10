using System.Windows;
using System.Windows.Controls;
using MillenniumLoadBalancer.TestApp.ViewModels;

namespace MillenniumLoadBalancer.TestApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void AddBackend_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.AddBackendAsync();
        }

        private async void ToggleBackend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BackendViewModel backend)
            {
                await ViewModel.ToggleBackendAsync(backend);
            }
        }

        private async void RemoveBackend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BackendViewModel backend)
            {
                await ViewModel.RemoveBackendAsync(backend);
            }
        }

        private void AddRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.NewRequestMessage))
            {
                var message = ViewModel.NewRequestMessage;
                ViewModel.NewRequestMessage = "";
                ViewModel.AddRequest(message);
            }
        }

        private void AddBulkRequests_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddBulkRequests();
        }

        private void RemoveRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is RequestViewModel request)
            {
                ViewModel.RemoveRequest(request);
            }
        }

        private async void SendAllRequests_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.SendAllRequestsAsync();
        }

        private void ClearAllRequests_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearAllRequests();
        }

        private void ClearTrace_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.TraceService.Clear();
        }
    }
}