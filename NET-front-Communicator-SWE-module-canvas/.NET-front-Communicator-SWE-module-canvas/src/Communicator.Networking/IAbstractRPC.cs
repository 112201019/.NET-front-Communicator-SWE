// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Communicator.Networking;

public interface IAbstractRPC
{
    void Subscribe(string methodName, Func<byte[], byte[]> method);

    Thread Connect(int portNumber);

    Task<byte[]> Call(string methodName, byte[] data);
}
