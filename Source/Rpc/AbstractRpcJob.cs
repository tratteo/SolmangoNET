// Copyright Siamango

using System;
using System.Threading.Tasks;

namespace SolmangoNET.Rpc;

public abstract class AbstractRpcJob
{
    public int JobRpcCalls { get; private set; }

    protected AbstractRpcJob(int jobRpcCalls)
    {
        JobRpcCalls = jobRpcCalls;
    }

    public abstract Task Execute();
}

public class RpcJob<T> : AbstractRpcJob
{
    private readonly Func<Task<T>> job;

    public Task<T> Task { get; private set; } = null;

    public RpcJob(Func<Task<T>> job, int jobRpcCalls) : base(jobRpcCalls)
    {
        this.job = job;
    }

    public RpcJobToken<T> GetToken() => new RpcJobToken<T>(this);

    public override async Task Execute()
    {
        Task = job.Invoke();
        await Task;
    }
}