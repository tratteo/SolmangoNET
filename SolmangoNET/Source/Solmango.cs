// Copyright Siamango

using OneOf;
using SolmangoNET.Exceptions;
using Solnet.Programs.Utilities;
using Solnet.Rpc;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SolmangoNET;

/// <summary>
///   Class containing useful methods (composed RPC request)
/// </summary>
public static class Solmango
{
    /// <summary>
    ///   Scrape mints addresses of a certain NFT collection specified by the name and/or symbol and/or update authority address.
    /// </summary>
    /// <param name="name"> Name of the collections, use <i> string.Empty </i> if not needed </param>
    /// <param name="symbol"> Symbol of the collections, use <i> string.Empty </i> if not needed </param>
    /// <param name="updateAuthority"> The address of the update authority address, use <i> null </i> if not needed </param>
    /// <returns> A list of all the mint addresses of the collection on success </returns>
    public static async Task<OneOf<List<(string, AccountKeyPair)>, SolmangoRpcException>> ScrapeCollectionMints(IRpcClient rpcClient, string name, string symbol, PublicKey updateAuthority)
    {
        if (name.Equals(string.Empty) && symbol.Equals(string.Empty) && updateAuthority.Equals(string.Empty)) return new List<(string, AccountKeyPair)>();
        List<MemCmp> filters = new List<MemCmp>();
        if (!name.Equals(string.Empty))
        {
            filters.Add(new()
            {
                Offset = 69,
                Bytes = Encoders.Base58.EncodeData(Encoding.UTF8.GetBytes(name))
            });
        }
        if (!symbol.Equals(string.Empty))
        {
            filters.Add(new()
            {
                Offset = 105,
                Bytes = Encoders.Base58.EncodeData(Encoding.UTF8.GetBytes(symbol))
            });
        }
        if (updateAuthority is not null)
        {
            filters.Add(new()
            {
                Offset = 1,
                Bytes = Encoders.Base58.EncodeData(updateAuthority.KeyBytes)
            });
        }

        List<(string, AccountKeyPair)> mints = new();
        var response = await rpcClient.GetProgramAccountsAsync(Programs.TOKEN_METADATA_PROGRAM_ID, Commitment.Finalized, null, filters.Count > 0 ? filters : null);
        if (response.WasRequestSuccessfullyHandled)
        {
            foreach (AccountKeyPair pair in response.Result)
            {
                string mint = ((ReadOnlySpan<byte>)Convert.FromBase64String(pair.Account.Data[0])).GetPubKey(33);
                mints.Add((mint, pair));
            }
            return mints;
        }
        else
        {
            return new SolmangoRpcException(response.Reason, response.ServerErrorCode);
        }
    }

    /// <summary>
    ///   Filter mints owned by a certain addres from a collection of mint addresses. Throws <see cref="SolmangoRpcException"/> on rpc fatal error.
    /// </summary>
    /// <param name="collection"> </param>
    /// <param name="owner"> </param>
    /// <returns> A list of all the mints of the specified collection, owned by the account </returns>
    public static async Task<OneOf<List<string>, SolmangoRpcException>> FilterMintsByOwner(IRpcClient rpcClient, ImmutableList<string> collection, PublicKey owner)
    {
        List<string> mintsCopy = new(collection);
        List<string> addressMints = new();
        var response = await rpcClient.GetTokenAccountsByOwnerAsync(owner, null, Programs.TOKEN_PROGRAM_ID);
        if (response.WasRequestSuccessfullyHandled)
        {
            foreach (TokenAccount account in response.Result.Value)
            {
                var index = mintsCopy.FindIndex(m => m.Equals(account.Account.Data.Parsed.Info.Mint));
                if (index >= 0 && int.Parse(account.Account.Data.Parsed.Info.TokenAmount.Amount) > 0)
                {
                    addressMints.Add(account.Account.Data.Parsed.Info.Mint);
                    mintsCopy.RemoveAt(index);
                }
            }
            return addressMints;
        }
        else
        {
            return new SolmangoRpcException(response.Reason, response.ServerErrorCode);
        }
    }

    /// <summary>
    ///   Calculate the snapshot for the Solana network
    /// </summary>
    /// <returns> The cluster snapshot containing information about the recent BlockHash and the FeeCalculator </returns>
    public static async Task<OneOf<ClusterSnapshot, SolmangoRpcException>> GetClusterSnapshot(IRpcClient rpcClient)
    {
        var blockResponse = await rpcClient.GetRecentBlockHashAsync();
        BlockHash blockHash;
        if (blockResponse.WasRequestSuccessfullyHandled)
        {
            blockHash = blockResponse.Result.Value;
        }
        else
        {
            return new SolmangoRpcException(blockResponse.Reason, blockResponse.ServerErrorCode);
        }

        var feesResponse = await rpcClient.GetFeesAsync();
        FeesInfo fees;
        if (feesResponse.WasRequestSuccessfullyHandled)
        {
            fees = feesResponse.Result.Value;
        }
        else
        {
            return new SolmangoRpcException(blockResponse.Reason, blockResponse.ServerErrorCode);
        }
        return new ClusterSnapshot() { BlockHash = blockHash, FeesInfo = fees };
    }

    /// <summary>
    ///   Calculate the dictionary containing the owners of each mint of a specified collection
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="collection"> </param>
    /// <returns> A dictionary with the owner address as key and the list of all his mints as value </returns>
    public static async Task<OneOf<Dictionary<string, List<string>>, SolmangoRpcException>> GetOwnersByCollection(IRpcClient rpcClient, ImmutableList<string> collection, IProgress<float> progressReport = null)
    {
        Dictionary<string, List<string>> owners = new();
        Stopwatch waitWatch = Stopwatch.StartNew();
        for (var i = 0; i < collection.Count; i++)
        {
            var mint = collection[i];
            waitWatch.Restart();
            // Get the mint largest account address
            var response = await rpcClient.GetTokenLargestAccountsAsync(mint);
            if (response.WasRequestSuccessfullyHandled)
            {
                // Get the largest account info
                var accountResponse = await rpcClient.GetAccountInfoAsync(response.Result.Value[0].Address);
                if (accountResponse.WasRequestSuccessfullyHandled)
                {
                    // Update the owner dictionary
                    string owner = ((ReadOnlySpan<byte>)Convert.FromBase64String(accountResponse.Result.Value.Data[0])).GetPubKey(32);
                    if (owners.ContainsKey(owner))
                    {
                        owners[owner].Add(mint);
                    }
                    else
                    {
                        owners.Add(owner, new List<string>() { mint });
                    }
                }
                else
                {
                    return new SolmangoRpcException(accountResponse.Reason, accountResponse.ServerErrorCode);
                }
            }
            else
            {
                return new SolmangoRpcException(response.Reason, response.ServerErrorCode);
            }
            waitWatch.Stop();
            if (waitWatch.ElapsedMilliseconds < 200)
            {
                await Task.Delay((int)(200 - waitWatch.ElapsedMilliseconds));
            }
            progressReport?.Report((float)i / collection.Count);
        }
        return owners;
    }
}