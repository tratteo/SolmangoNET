// Copyright Siamango

using OneOf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SolmangoNET.Rpc;

public class BasicRpcScheduler : IRpcScheduler
{
    private readonly Queue<AbstractRpcJob> rpcJobs;
    private readonly int rpcCallDelay;
    private readonly int maxEnqueuableRequests;
    private bool running = false;
    private Thread? schedulerThread;

    public int JobsCount => rpcJobs.Count;

    public BasicRpcScheduler(int maxEnqueuableRequests, int rpcCallDelay = 100)
    {
        rpcJobs = new Queue<AbstractRpcJob>();
        this.rpcCallDelay = rpcCallDelay;
        this.maxEnqueuableRequests = maxEnqueuableRequests;
    }

    public void Interrupt()
    {
        if (!running) return;
        running = false;
        schedulerThread?.Join();
    }

    public void Start()
    {
        if (running) return;
        running = true;
        schedulerThread = new Thread(Scheduler);
        schedulerThread.Start();
    }

    public OneOf<RpcJobToken<T>, RpcBatcherSaturatedException> Schedule<T>(Func<Task<T>> job, int jobRpcCalls = 1)
    {
        if (JobsCount >= maxEnqueuableRequests)
        {
            return new RpcBatcherSaturatedException();
        }

        RpcJob<T> scheduled = new RpcJob<T>(job, jobRpcCalls);
        lock (rpcJobs)
        {
            rpcJobs.Enqueue(scheduled);
        }
        return scheduled.GetToken();
    }

    private async void Scheduler()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (running)
        {
            if (rpcJobs.Count > 0)
            {
                AbstractRpcJob? job = null;
                lock (rpcJobs)
                {
                    job = rpcJobs.Dequeue();
                }
                stopwatch.Restart();
                await job.Execute();
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds < rpcCallDelay * job.JobRpcCalls)
                {
                    Thread.Sleep((rpcCallDelay * job.JobRpcCalls) - (int)stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }
}