// Copyright Siamango

using Solnet.Rpc.Models;

namespace SolmangoNET;

public readonly struct ClusterSnapshot
{
    public readonly LatestBlockHash blockHash;

    public readonly FeesInfo feesInfo;

    public ClusterSnapshot(LatestBlockHash blockHash, FeesInfo feesInfo)
    {
        this.blockHash = blockHash;
        this.feesInfo = feesInfo;
    }
}