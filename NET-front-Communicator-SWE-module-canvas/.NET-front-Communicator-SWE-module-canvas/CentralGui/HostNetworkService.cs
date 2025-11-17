using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CanvasDataModel;
using ViewModel;

namespace CentralGui;

public class HostNetworkService : INetworkService
{
    public event Action<NetworkMessage>? MessageReceived;
    private readonly TcpListener _listener;
    private readonly List<TcpClient> _clients = new();

    public HostNetworkService()
    {
        _listener = new TcpListener(IPAddress.Loopback, 12345);
        _listener.Start();
        Task.Run(AcceptClientsAsync);
        MessageBox.Show("Host started on port 12345. Waiting for clients...", "Host");
    }

    private async Task AcceptClientsAsync()
    {
        while (true)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                lock (_clients)
                {
                    _clients.Add(client);
                }
                Debug.WriteLine("[NETWORK] Client connected.");
                Task.Run(() => ListenToClientAsync(client));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NETWORK] Error accepting client: {ex.Message}");
            }
        }
    }

    private async Task ListenToClientAsync(TcpClient client)
    {
        StreamReader reader = new StreamReader(client.GetStream(), Encoding.UTF8);
        try
        {
            while (client.Connected)
            {
                string? json = await reader.ReadLineAsync();
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
            Debug.WriteLine($"[NETWORK] Client disconnected: {ex.Message}");
        }
        finally
        {
            lock (_clients)
            {
                _clients.Remove(client);
            }
            client.Close();
        }
    }

    public void SendMessage(NetworkMessage message)
    {
        string json = CanvasDataModelSerializer.SerializeMessage(message);
        lock (_clients)
        {
            foreach (var client in _clients)
            {
                try
                {
                    StreamWriter writer = new StreamWriter(client.GetStream(), Encoding.UTF8);
                    writer.WriteLine(json);
                    writer.Flush();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NETWORK] Failed to send to client: {ex.Message}");
                }
            }
        }
    }
}
