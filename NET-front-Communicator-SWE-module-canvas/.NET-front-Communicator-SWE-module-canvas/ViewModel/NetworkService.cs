using System;
using CanvasDataModel;
namespace ViewModel;

/// <summary>
/// A placeholder for a real network. In this simulation, it
/// holds static references to the VMs and routes messages between them.
/// </summary>
public static class NetworkService
{
    // In a real app, these would be network endpoints
    public static HostViewModel? Host { get; set; }
    public static ClientViewModel? Client { get; set; }

    /// <summary>
    /// A Client sends a message to the Host.
    /// </summary>
    public static void SendMessage(NetworkMessage message)
    {
        Console.WriteLine($"[NETWORK] Client ({message.SenderId}) -> Host: {message.MsgType}");
        // Simulate network latency (optional)
        // await Task.Delay(50); 

        // Route the message to the Host's processing logic
        Host?.ProcessIncomingMessage(message);
    }

    /// <summary>
    /// The Host broadcasts a message to all Clients.
    /// </summary>
    public static void Broadcast(NetworkMessage message)
    {
        Console.WriteLine($"[NETWORK] Host -> Client ({Client?.CurrentUserId}): {message.MsgType}");
        // Simulate network latency (optional)
        // await Task.Delay(50);

        // Route the message to the Client's processing logic
        Client?.ProcessIncomingMessage(message);
    }
}
