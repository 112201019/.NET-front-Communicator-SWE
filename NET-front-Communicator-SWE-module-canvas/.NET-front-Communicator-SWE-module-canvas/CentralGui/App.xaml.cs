using System.Windows;
using ViewModel;

namespace CentralGui
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Create ViewModels
            var hostVM = new HostViewModel();
            var clientVM = new ClientViewModel();

            // 2. Create Host Window
            var hostWindow = new MainWindow();
            hostWindow.Title = "HOST (127.0.0.1)";
            hostWindow.DataContext = hostVM;
            hostWindow.Show();

            // 3. Create Client Window
            var clientWindow = new MainWindow();
            clientWindow.Title = "CLIENT (192.168.1.50)";
            clientWindow.Left = hostWindow.Left + hostWindow.Width + 20; // Position next to host
            clientWindow.DataContext = clientVM;
            clientWindow.Show();
        }
    }
}
