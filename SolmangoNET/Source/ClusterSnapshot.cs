// Copyright Siamango

using Solnet.Rpc.Models;

namespace SolmangoNET;

public readonly struct ClusterSnapshot
{
    public readonly BlockHash blockHash;

    public readonly FeesInfo feesInfo;

    public ClusterSnapshot(BlockHash blockHash, FeesInfo feesInfo)
    {
        this.blockHash = blockHash;
        this.feesInfo = feesInfo;
    }
}