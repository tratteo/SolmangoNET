// Copyright Siamango

using Solnet.Rpc.Models;

namespace SolmangoNET;

public class ClusterSnapshot
{
    public BlockHash BlockHash { get; init; }

    public FeesInfo FeesInfo { get; init; }
}