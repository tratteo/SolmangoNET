﻿// Copyright Siamango

using OneOf;
using Solana.Metaplex;
using SolmangoNET.Exceptions;
using Solnet.Programs;
using Solnet.Programs.Utilities;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Threading;
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
    public static async Task<OneOf<List<(string, AccountKeyPair)>, SolmangoRpcException>> ScrapeCollectionMints(IRpcClient rpcClient, string? name, string? symbol, PublicKey? updateAuthority)
    {
        if (name is null && symbol is null && updateAuthority is null)
            return new List<(string, AccountKeyPair)>();
        var filters = new List<MemCmp>();
        if (name is not null)
        {
            filters.Add(new()
            {
                Offset = 69,
                Bytes = Encoders.Base58.EncodeData(Encoding.UTF8.GetBytes(name))
            });
        }
        if (symbol is not null)
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
        var response = await rpcClient.GetProgramAccountsAsync(MetadataProgram.ProgramIdKey, Commitment.Finalized, null, filters.Count > 0 ? filters : null);
        if (response.WasRequestSuccessfullyHandled)
        {
            foreach (var pair in response.Result)
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
        var response = await rpcClient.GetTokenAccountsByOwnerAsync(owner, null, TokenProgram.ProgramIdKey);
        if (response.Result is not null && response.WasRequestSuccessfullyHandled)
        {
            foreach (var account in response.Result.Value)
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
    ///   Get the associated token account
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="owner"> </param>
    /// <param name="tokenMint"> </param>
    /// <returns> </returns>
    public static async Task<PublicKey?> GetAssociatedTokenAccount(IRpcClient rpcClient, string owner, string tokenMint)
    {
        var associatedAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(new PublicKey(owner), new PublicKey(tokenMint));
        var res = await rpcClient.GetTokenAccountBalanceAsync(associatedAccount);
        return res.WasRequestSuccessfullyHandled ? associatedAccount : null;
    }

    /// <summary>
    ///   Calculate the snapshot for the Solana network
    /// </summary>
    /// <returns> The cluster snapshot containing information about the recent BlockHash and the FeeCalculator </returns>
    public static async Task<OneOf<ClusterSnapshot, SolmangoRpcException>> GetClusterSnapshot(IRpcClient rpcClient)
    {
        var blockResponse = await rpcClient.GetLatestBlockHashAsync();
        LatestBlockHash blockHash;
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
        return new ClusterSnapshot(blockHash, fees);
    }

    /// <summary>
    ///   Calculate the dictionary containing the owners of each mint of a specified collection. Bound the requests rate to 10/s
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="collection"> </param>
    /// <returns> A dictionary with the owner address as key and the list of all his mints as value </returns>
    public static async Task<OneOf<Dictionary<string, List<string>>, SolmangoRpcException>> GetOwnersByCollection(IRpcClient rpcClient, ImmutableList<string> collection, IProgress<double>? progressReport = null)
    {
        Dictionary<string, List<string>> owners = new();
        var waitWatch = Stopwatch.StartNew();
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

    /// <summary>
    ///   Calculate the dictionary containing the owners of each mint of a specified collection. Requests are in parallel and rate is unbounded
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="collection"> </param>
    /// <returns> A dictionary with the owner address as key and the list of all his mints as value </returns>
    public static async Task<Dictionary<string, List<string>>> GetOwnersByCollectionParallel(IRpcClient rpcClient, ImmutableList<string> collection, IProgress<double>? progressReport = null)
    {
        Dictionary<string, List<string>> owners = new();
        var counter = 0;

        await Parallel.ForEachAsync(collection, async (mint, token) =>
        {
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
                    lock (owners)
                    {
                        if (owners.ContainsKey(owner))
                        {
                            owners[owner].Add(mint);
                        }
                        else
                        {
                            owners.Add(owner, new List<string>() { mint });
                        }
                    }
                }
            }
            Interlocked.Increment(ref counter);
            progressReport?.Report((float)counter / collection.Count);
        });
        return owners;
    }

    /// <summary>
    ///   Sends a custom SPL token. Handles the associated token account
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="receiver"> </param>
    /// <param name="sender"> </param>
    /// <param name="tokenMint"> </param>
    /// <param name="amount"> The amount in Sol to send </param>
    /// <returns> </returns>
    public static async Task<OneOf<bool, SolmangoRpcException>> SendSplToken(IRpcClient rpcClient, Account sender, string receiver, string tokenMint, double amount)
    {
        // Get the blockhash
        var blockHash = await rpcClient.GetLatestBlockHashAsync();
        if (blockHash is null) return false;
        if (!blockHash.WasRequestSuccessfullyHandled) return new SolmangoRpcException(blockHash.Reason, blockHash.ServerErrorCode);

        // Get the sender ata
        var fromAta = await GetAssociatedTokenAccount(rpcClient, sender.PublicKey, tokenMint);
        if (fromAta is null) return false;

        // Get the token decimals and the actual amount
        var res = await rpcClient.GetTokenSupplyAsync(tokenMint);
        if (res is null) return false;
        if (!res.WasRequestSuccessfullyHandled) return new SolmangoRpcException(res.Reason, res.ServerErrorCode);

        var actualAmount = (ulong)(amount * Math.Pow(10, res.Result.Value.Decimals));

        byte[] transaction;
        // Get or create the receiver ata
        var toAta = await GetAssociatedTokenAccount(rpcClient, receiver, tokenMint);
        if (toAta is null)
        {
            toAta = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(new PublicKey(receiver), new PublicKey(tokenMint));
            transaction = new TransactionBuilder()
               .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
               .SetFeePayer(sender)
               .AddInstruction(
                AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                   sender,
                   new PublicKey(receiver),
                   new PublicKey(tokenMint)))
               .AddInstruction(TokenProgram.Transfer(
                   fromAta,
                   toAta,
                   actualAmount,
                   sender.PublicKey))
               .Build(sender);
        }
        else
        {
            transaction = new TransactionBuilder()
            .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
            .SetFeePayer(sender)
            .AddInstruction(TokenProgram.Transfer(
                fromAta,
                toAta,
                actualAmount,
                sender.PublicKey))
            .Build(sender);
        }

        // Perform transaction
        var result = await rpcClient.SendTransactionAsync(Convert.ToBase64String(transaction));
        return result is null
            ? (OneOf<bool, SolmangoRpcException>)false
            : !result.WasRequestSuccessfullyHandled ? new SolmangoRpcException(result.Reason, result.ServerErrorCode) : true;
    }
}