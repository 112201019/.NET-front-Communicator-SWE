using System;
using CanvasDataModel;

namespace CanvasDataModel;

/// <summary>
/// An interface to abstract the network layer, allowing
/// VMs to send/receive messages without knowing if it's
/// TCP, a simulation, or something else.
/// </summary>
public interface INetworkService
{
    /// <summary>
    /// Fires when a new message is received from the network.
    /// </summary>
    event Action<NetworkMessage> MessageReceived;

    /// <summary>
    /// Sends a message over the network.
    /// Host: Broadcasts to all clients.
    /// Client: Sends to Host.
    /// </summary>
    void SendMessage(NetworkMessage message);
}
