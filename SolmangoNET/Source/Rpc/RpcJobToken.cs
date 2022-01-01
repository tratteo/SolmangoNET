// Copyright Siamango

using System;
using System.Runtime.CompilerServices;

namespace SolmangoNET.Rpc;

public struct RpcJobToken<T>
{
    private readonly RpcJob<T> completionSource;

    public RpcJobToken(RpcJob<T> completionSource)
    {
        this.completionSource = completionSource;
    }

    public Awaiter GetAwaiter() => new Awaiter(completionSource);

    public struct Awaiter : INotifyCompletion
    {
        private readonly RpcJob<T> completionSource;

        public bool IsCompleted => completionSource.Task != null && completionSource.Task.IsCompleted;

        public Awaiter(RpcJob<T> completionSource)
        {
            this.completionSource = completionSource;
        }

        public void OnCompleted(Action continuation) => continuation?.Invoke();

        public T GetResult()
        {
            while (completionSource.Task == null || !completionSource.Task.IsCompleted)
            {
            }
            return completionSource.Task.Result;
        }
    }
}