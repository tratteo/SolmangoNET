// Copyright Siamango

using OneOf;
using System;
using System.Threading.Tasks;

namespace SolmangoNET.Rpc;

public interface IRpcScheduler
{
    public int JobsCount { get; }

    public void Interrupt();

    public void Start();

    public OneOf<RpcJobToken<T>, RpcBatcherSaturatedException> Schedule<T>(Func<Task<T>> job, int jobRpcCalls = 1);
}