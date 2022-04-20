// Copyright Siamango

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
        if (response.WasRequestSuccessfullyHandled)
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
    ///   Tries to retrive the associated token account of the given mint on a specified address
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="address"> </param>
    /// <param name="tokenMint"> </param>
    /// <returns> </returns>
    public static async Task<string?> TryGetAssociatedTokenAccount(IRpcClient rpcClient, string address, string tokenMint)
    {
        var res = await rpcClient.GetTokenAccountsByOwnerAsync(address, tokenMint);
        return res.Result is null ? null : res.Result.Value is not null && res.Result.Value.Count > 0 ? res.Result.Value[0].PublicKey : null;
    }

    /// <summary>
    ///   Sends a custom SPL token. Create the address on the receiver account if does not exists.
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="toPublicKey"> </param>
    /// <param name="fromAccount"> </param>
    /// <param name="tokenMint"> </param>
    /// <param name="amount"> The amount in Sol to send </param>
    /// <returns> </returns>
    public static async Task<OneOf<bool, SolmangoRpcException>> SendSplToken(IRpcClient rpcClient, Account fromAccount, string toPublicKey, string tokenMint, ulong amount)
    {
        var blockHash = await rpcClient.GetLatestBlockHashAsync();
        var rentExemptionAmmount = await rpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize);
        var first = TryGetAssociatedTokenAccount(rpcClient, toPublicKey, tokenMint);
        var second = TryGetAssociatedTokenAccount(rpcClient, fromAccount.PublicKey, tokenMint);
        await Task.WhenAll(first, second);
        var res = await rpcClient.GetTokenSupplyAsync(tokenMint);
        if (!res.WasRequestSuccessfullyHandled) return false;
        var associatedAccount = first.Result;
        var sourceTokenAccount = second.Result;
        if (sourceTokenAccount is null) return false;
        byte[] transaction;

        var actualAmount = res.Result.Value.Decimals == 0 ? amount : amount * (ulong)res.Result.Value.Decimals;

        if (associatedAccount is not null)
        {
            transaction = new TransactionBuilder().SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(fromAccount)
                .AddInstruction(TokenProgram.Transfer(new PublicKey(sourceTokenAccount),
                new PublicKey(associatedAccount),
                actualAmount,
                fromAccount.PublicKey))
                .Build(fromAccount);
        }
        else
        {
            var newAccKeypair = new Account();
            transaction = new TransactionBuilder().SetRecentBlockHash(blockHash.Result.Value.Blockhash).
                SetFeePayer(fromAccount).
                AddInstruction(
                SystemProgram.CreateAccount(
                    fromAccount.PublicKey,
                    newAccKeypair.PublicKey,
                    rentExemptionAmmount.Result,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey)).
                AddInstruction(
                TokenProgram.InitializeAccount(
                    newAccKeypair.PublicKey,
                    new PublicKey(tokenMint),
                    new PublicKey(toPublicKey))).
                AddInstruction(TokenProgram.Transfer(new PublicKey(sourceTokenAccount),
                    newAccKeypair.PublicKey,
                    actualAmount,
                    fromAccount.PublicKey))
                .Build(new List<Account>()
                {
                        fromAccount,
                        newAccKeypair
                });
        }
        var result = await rpcClient.SendTransactionAsync(Convert.ToBase64String(transaction));
        return !res.WasRequestSuccessfullyHandled ? new SolmangoRpcException(res.Reason, res.ServerErrorCode) : true;
    }
}