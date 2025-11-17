using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CanvasDataModel;
using ViewModel;

namespace CentralGui;

public class ClientNetworkService : INetworkService
{
    public event Action<NetworkMessage>? MessageReceived;
    private readonly TcpClient _client;
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;

    public ClientNetworkService()
    {
        try
        {
            _client = new TcpClient("127.0.0.1", 12345);
            var stream = _client.GetStream();
            _writer = new StreamWriter(stream, Encoding.UTF8);
            _reader = new StreamReader(stream, Encoding.UTF8);

            Task.Run(ListenToHostAsync);
            MessageBox.Show("Connected to host!", "Client");
        }
        catch (Exception)
        {
            MessageBox.Show("Could not connect to host. Make sure host is running.", "Connection Error");
            Application.Current.Shutdown();
            throw;
        }
    }

    private async Task ListenToHostAsync()
    {
        try
        {
            while (_client.Connected)
            {
                string? json = await _reader.ReadLineAsync();
                if (json != null)
                {
                    NetworkMessage? message = CanvasDataModelSerializer.DeserializeMessage(json);
                    if (message != null)
                    {
                        // ---
                        // --- THE FIX ---
                        // ---
                        // We must run this on the UI thread, otherwise
                        // the ViewModel will crash when it tries to update the canvas.
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageReceived?.Invoke(message);
                        });
                        // ---
                        // --- END FIX ---
                        // ---
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NETWORK] Disconnected from host: {ex.Message}");
        }
    }

    public void SendMessage(NetworkMessage message)
    {
        try
        {
            string json = CanvasDataModelSerializer.SerializeMessage(message);
            _writer.WriteLine(json);
            _writer.Flush();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NETWORK] Failed to send message to host: {ex.Message}");
        }
    }
}
