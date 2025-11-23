// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Communicator.Networking;
public class NetworkFront : IController, INetworking
{
    /** Variable to store the function mappings. */
    private Dictionary<int, IMessageListener> _listeners;

    /** Variable to track the number of functions. */
    private int _functionCount = 1;
    /** Variable to store the RPC. */
    private IAbstractRPC _moduleRpc = null;
    public void SendData(byte[] data, ClientNode[] dest, int module, int priority)
    {
        int dataLength = data.Length;
        int destSize = 0;
        foreach (ClientNode record in dest)
        {
            byte[] hostName = Encoding.UTF8.GetBytes(record.HostName);
            destSize += 1 + hostName.Length + sizeof(int); // 1 byte length + host + port
        }
        // 1 - data length 1 - dest count 1 - module 1 - priority
        int bufferSize = dataLength + destSize + 4 * sizeof(int);
        MemoryStream buffer = new MemoryStream(bufferSize);
        BinaryWriter writer = new BinaryWriter(buffer);
        writer.Write(dest.Length);
        foreach (ClientNode record in dest)
        {
            byte[] hostName = Encoding.UTF8.GetBytes(record.HostName);
            writer.Write((byte)hostName.Length);
            writer.Write(hostName);
            writer.Write(record.Port);
        }
        writer.Write(dataLength);
        writer.Write(data);
        writer.Write(module);
        writer.Write(priority);
        byte[] args = buffer.ToArray();

        _moduleRpc.Call("networkRPCSendData", args);
    }

    public void Broadcast(byte[] data, int module, int priority)
    {
        int dataLength = data.Length;
        int bufferSize = dataLength + 3 * sizeof(int);
        MemoryStream buffer = new MemoryStream(bufferSize);
        BinaryWriter writer = new BinaryWriter(buffer);
        writer.Write(dataLength);
        writer.Write(data);
        writer.Write(module);
        writer.Write(priority);
        byte[] args = buffer.ToArray();

        _moduleRpc.Call("networkRPCBroadcast", args);
    }

    public void Subscribe(int name, IMessageListener function)
    {
        _listeners.Add(name, function);
        int bufferSize = sizeof(int);
        MemoryStream buffer = new MemoryStream(bufferSize);
        BinaryWriter writer = new BinaryWriter(buffer);
        writer.Write(name);
        string callbackName = "callback" + name;
        byte[] args = buffer.ToArray();

        _moduleRpc.Subscribe(callbackName, (byte[] args) => {
            function.ReceiveData(args);
            return null;
        });
    }

    public void RemoveSubscription(int name)
    {
        int bufferSize = sizeof(int);
        MemoryStream buffer = new MemoryStream(bufferSize);
        BinaryWriter writer = new BinaryWriter(buffer);
        writer.Write(name);
        byte[] args = buffer.ToArray();

        _moduleRpc.Call("networkRPCRemoveSubscription", args);
    }

    public void AddUser(ClientNode deviceAddress, ClientNode mainServerAddress)
    {
        int bufferSize = 2 + deviceAddress.HostName.Length + mainServerAddress.HostName.Length
                + 2 * sizeof(int);
        MemoryStream buffer = new MemoryStream(bufferSize);
        BinaryWriter writer = new BinaryWriter(buffer);
        byte[] hostName = Encoding.UTF8.GetBytes(deviceAddress.HostName);
        writer.Write((byte)hostName.Length);
        writer.Write(hostName);
        writer.Write(deviceAddress.Port);
        hostName = Encoding.UTF8.GetBytes(mainServerAddress.HostName);
        writer.Write((byte)hostName.Length);
        writer.Write(hostName);
        writer.Write(mainServerAddress.Port);
        byte[] args = buffer.ToArray();

        _moduleRpc.Call("getNetworkRPCAddUser", args);
    }

    /**
     * Function to call the subscriber in frontend.
     *
     * @param data the data to send
     */
    public void NetworkFrontCallSubscriber(byte[] data)
    {
        int dataSize = data.Length - 1;
        MemoryStream buffer = new MemoryStream(data);
        BinaryReader reader = new BinaryReader(buffer);
        int module = reader.ReadInt32();
        byte[] newData = new byte[dataSize];
        IMessageListener function = _listeners.GetValueOrDefault(module);
        function?.ReceiveData(newData);
    }

    public void CloseNetworking()
    {
        Console.WriteLine("Closing Networking in front...");
        _moduleRpc.Call("networkRPCCloseNetworking", new byte[0]);
    }

    public void ConsumeRPC(IAbstractRPC rpc)
    {
        _moduleRpc = rpc;
        foreach (KeyValuePair<int, IMessageListener> listener in _listeners)
        {
            int key = listener.Key;
            byte[] args = BitConverter.GetBytes(key);

            // Call the RPC method asynchronously
            _moduleRpc.Call("networkRPCSubscribe", args);
        }
    }
}
