using System.Configuration;
using System.Data;
using System.Windows;
using CanvasDataModel;
using ViewModel; // <-- ADDED

namespace CentralGui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // --- NEW STARTUP LOGIC ---
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Ask the user if they are Host or Client
        string title = "Network Mode";
        string question = "Do you want to run as Host or Client?";
        MessageBoxResult result = MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Question);

        BaseCanvasViewModel viewModel;
        INetworkService networkService;

        if (result == MessageBoxResult.Yes)
        {
            // --- HOST ---
            // Create Host network and VM
            var hostNetwork = new HostNetworkService();
            networkService = hostNetwork;
            viewModel = new HostViewModel("Host-User", networkService);
        }
        else
        {
            // --- CLIENT ---
            try
            {
                // Create Client network and VM
                var clientNetwork = new ClientNetworkService();
                networkService = clientNetwork;
                viewModel = new ClientViewModel("Client-1", networkService);
            }
            catch (System.Exception)
            {
                // ClientNetworkService throws error if host isn't running
                // App.Current.Shutdown() is called inside it
                return;
            }
        }

        // 2. Create the MainWindow and pass the correct VM and Network
        MainWindow window = new MainWindow(viewModel, networkService);
        window.Show();
    }
    // --- END NEW ---
}
