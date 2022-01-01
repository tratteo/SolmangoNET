// Copyright Siamango

using System;

namespace SolmangoNET.Rpc;

public class RpcBatcherSaturatedException : Exception
{
    public RpcBatcherSaturatedException()
    {
    }

    public RpcBatcherSaturatedException(string message) : base(message)
    {
    }
}