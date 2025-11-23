// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communicator.Networking;
public interface INetworking
{
    /**
     * Function to send data to given list of destination.
     *
     * @param data     the data to be sent
     * @param dest     the destination to send the data
     * @param module   the module to send to
     * @param priority the priority of the data
     */
    void SendData(byte[] data, ClientNode[] dest, int module, int priority);

    /**
     * Function to send data to all clients.
     *
     * @param data     the data to be sent
     * @param module   the module to be sent to
     * @param priority the priority of the data
     */
    void Broadcast(byte[] data, int module, int priority);

    /**
     * Function to subscribe a function to the network.
     *
     * @param name     the name of the module.
     * @param function the function to invoke on receiving the packet
     */
    void Subscribe(int name, IMessageListener function);

    /**
     * Functiont to remove from subscription.
     *
     * @param name the name of the module
     */
    void RemoveSubscription(int name);
}
