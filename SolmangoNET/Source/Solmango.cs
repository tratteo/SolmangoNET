// Copyright Siamango

using OneOf;
using Solana.Metaplex;
using SolmangoNET.Exceptions;
using SolmangoNET.Rpc;
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
    ///   Filter mints owned by a certain address from a collection of mint addresses. Throws <see cref="SolmangoRpcException"/> on rpc
    ///   fatal error.
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
    /// <returns> The associated token account, null if not found </returns>
    public static async Task<PublicKey?> GetAssociatedTokenAccount(IRpcClient rpcClient, string owner, string tokenMint)
    {
        var associatedAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(new PublicKey(owner), new PublicKey(tokenMint));
        var res = await rpcClient.GetTokenAccountBalanceAsync(associatedAccount);
        return res.WasRequestSuccessfullyHandled ? associatedAccount : null;
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
        var scheduler = new BasicRpcScheduler(collection.Count, 100);
        scheduler.Start();
        for (var i = 0; i < collection.Count; i++)
        {
            var mint = collection[i];
            // Get the mint largest account address
            var oneOf = scheduler.Schedule(() => rpcClient.GetTokenLargestAccountsAsync(mint));
            if (oneOf.TryPickT1(out var satEx, out var jobToken))
            {
                return new SolmangoRpcException("Scheduler saturated", 0x0);
            }

            var response = await jobToken;
            if (!response.WasRequestSuccessfullyHandled)
            {
                return new SolmangoRpcException(response.Reason, response.ServerErrorCode);
            }
            // Get the largest account info
            var accountResponseOneOf = scheduler.Schedule(() => rpcClient.GetAccountInfoAsync(response.Result.Value[0].Address));
            if (accountResponseOneOf.TryPickT1(out satEx, out var accountJobToken))
            {
                return new SolmangoRpcException("Scheduler saturated", 0x0);
            }
            var accountResponse = await accountJobToken;
            if (!accountResponse.WasRequestSuccessfullyHandled)
            {
                return new SolmangoRpcException(accountResponse.Reason, accountResponse.ServerErrorCode);
            }

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

            progressReport?.Report((float)i / collection.Count);
        }

        scheduler.Interrupt();
        return owners;
    }

    /// <summary>
    ///   Calculate the dictionary containing the owners of each mint of a specified collection. Send requests in batches to greatly speed
    ///   up the process.
    ///   <para> Use unbounded endpoints, look at <see href="https://www.genesysgo.com/"/> </para>
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="collection"> </param>
    /// <param name="progressReport"> </param>
    /// <param name="batchSizeTrigger"> </param>
    /// <returns> A dictionary with the owner address as key and the list of all his mints as value </returns>
    public static OneOf<Dictionary<string, List<string>>, SolmangoRpcException> GetOwnersByCollectionBatch(IRpcClient rpcClient, ImmutableList<string> collection, IProgress<double>? progressReport = null, int batchSizeTrigger = 100)
    {
        Dictionary<string, List<string>> owners = new();
        var batcher = new SolanaRpcBatchWithCallbacks(rpcClient);
        batcher.AutoExecute(BatchAutoExecuteMode.ExecuteWithCallbackFailures, batchSizeTrigger);
        for (var i = 0; i < collection.Count; i++)
        {
            var mint = collection[i];
            var filters = new List<MemCmp>()
            {
            new MemCmp()
            {
                Offset = 0,
                Bytes = Encoders.Base58.EncodeData(new PublicKey(mint).KeyBytes)
            }
            };
            // Get the mint largest account address
            batcher.GetProgramAccounts(TokenProgram.ProgramIdKey, Commitment.Finalized, TokenProgram.TokenAccountDataSize, filters, (listKeyPair, ex) =>
            {
                if (ex is not null)
                {
                    Console.WriteLine(ex.Message);
                    return;
                }
                for (var j = 0; j < listKeyPair.Count; j++)
                {
                    var pair = listKeyPair[j];
                    progressReport?.Report((float)i / collection.Count);
                    var bytes = Convert.FromBase64String(pair.Account.Data[0]);
                    var balance = ((ReadOnlySpan<byte>)bytes).GetU64(64);
                    if (balance <= 0) continue;
                    string owner = ((ReadOnlySpan<byte>)bytes).GetPubKey(32);
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
            });
        }
        batcher.Flush();
        return owners;
    }

    /// <summary>
    ///   Get all the holders of the specified spl token
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="tokenMint"> </param>
    /// <returns> The list of all the holders </returns>
    public static async Task<OneOf<List<AccountKeyPair>, SolmangoRpcException>> GetSplTokenHolders(IRpcClient rpcClient, string tokenMint)
    {
        var filters = new List<MemCmp>()
        {
            new MemCmp()
            {
                Offset = 0,
                Bytes = Encoders.Base58.EncodeData(new PublicKey(tokenMint).KeyBytes)
            }
        };
        var res = await rpcClient.GetProgramAccountsAsync(TokenProgram.ProgramIdKey, Commitment.Finalized, TokenProgram.TokenAccountDataSize, filters);
        return res.WasRequestSuccessfullyHandled ? res.Result : new SolmangoRpcException(res.Reason, res.ServerErrorCode);
    }

    /// <summary>
    ///   Sends a multiple transactions in batch to greatly speed up the process.
    ///   <para> Use unbounded endpoints, look at <see href="https://www.genesysgo.com/"/> </para>
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="transactions"> All the transactions </param>
    /// <param name="batchSize"> The amount of requests in a single batch </param>
    /// <returns> A list of tuples identifying the transaction and whether it has been succesfully sent to the cluster </returns>
    public static async Task<List<(string transaction, bool success)>> SendTransactionBatch(IRpcClient rpcClient, List<byte[]> transactions, int batchSize = 100)
    {
        var batcher = new SolanaRpcBatchWithCallbacks(rpcClient);
        batcher.AutoExecute(BatchAutoExecuteMode.ExecuteWithCallbackFailures, batchSize);

        var results = new List<(string transaction, bool success)>();
        foreach (var transaction in transactions)
        {
            batcher.SendTransaction(transaction, false, Commitment.Finalized, (res, ex) => results.Add((res, ex is null)));
        }
        // This call is actually blocking, so the function needs to be async in order not to block
        batcher.Flush();

        // Removes compiler warning
        await Task.CompletedTask;

        // Return a list of all transaction with a bool identifying if they are successful or not. Returning the transactions strings allows
        // to subscribe to their confirmation
        return results;
    }

    /// <summary>
    ///   Builds a transaction to send an SPL token.
    ///   <para> Use it with <see cref="SendTransactionBatch"/> in order to batch multiple transactions. </para>
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="sender"> </param>
    /// <param name="receiver"> </param>
    /// <param name="tokenMint"> </param>
    /// <param name="amount"> </param>
    /// <returns> The transaction hash upon success </returns>
    public static async Task<OneOf<byte[], Exception>> BuildSendSplTokenTransaction(IRpcClient rpcClient, Account sender, string receiver, string tokenMint, double amount)
    {
        // Get the blockhash
        var blockHash = await rpcClient.GetLatestBlockHashAsync();
        if (blockHash is null) return new Exception("BlockHash can't be null");
        if (!blockHash.WasRequestSuccessfullyHandled) return new SolmangoRpcException(blockHash.Reason, blockHash.ServerErrorCode);

        // Get the sender ata
        var fromAta = await GetAssociatedTokenAccount(rpcClient, sender.PublicKey, tokenMint);
        if (fromAta is null) return new Exception("Sender ata can't be null");

        // Get the token decimals and the actual amount
        var res = await rpcClient.GetTokenSupplyAsync(tokenMint);
        if (res is null) return new Exception("Token decimal can't be null");
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

        return transaction;
    }

    /// <summary>
    ///   Sends a custom SPL token. Handles the associated token account
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="receiver"> </param>
    /// <param name="sender"> </param>
    /// <param name="tokenMint"> </param>
    /// <param name="amount"> The amount in Sol to send </param>
    /// <returns> The signature of the transaction upon success </returns>
    public static async Task<OneOf<string, Exception>> SendSplToken(IRpcClient rpcClient, Account sender, string receiver, string tokenMint, double amount)
    {
        var res = await BuildSendSplTokenTransaction(rpcClient, sender, receiver, tokenMint, amount);
        if (res.TryPickT1(out var ex, out var transaction)) return ex;
        var result = await rpcClient.SendTransactionAsync(transaction);
        // Return the signature of the transaction, so that it is possible to subscribe to its confirmation
        return result is null ?
            new Exception("Transaction signature can't be null") :
                result.WasRequestSuccessfullyHandled ?
                    result.Result :
                    new SolmangoRpcException(result.Reason, result.ServerErrorCode);
    }
}