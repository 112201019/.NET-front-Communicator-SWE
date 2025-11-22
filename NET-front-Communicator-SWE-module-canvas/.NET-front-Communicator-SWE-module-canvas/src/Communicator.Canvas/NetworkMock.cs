using System;
using System.Collections.Generic;

namespace Communicator.Canvas;

public static class NetworkMock
{
    // Registry of listeners
    private static Dictionary<string, Action<string>> _listeners = new();

    public static void Register(string ip, Action<string> listener)
    {
        _listeners[ip] = listener;
    }

    public static void SendMessage(string targetIp, string jsonMessage)
    {
        Console.WriteLine($"[Network] Sending to {targetIp}");
        if (_listeners.ContainsKey(targetIp))
        {
            _listeners[targetIp].Invoke(jsonMessage);
        }
    }

    public static void Broadcast(List<string> clientIps, string jsonMessage)
    {
        Console.WriteLine($"[Network] Broadcasting");
        foreach (string ip in clientIps)
        {
            SendMessage(ip, jsonMessage);
        }
    }
}
