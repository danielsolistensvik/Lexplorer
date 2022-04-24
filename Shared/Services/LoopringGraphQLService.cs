﻿using Lexplorer.Helpers;
using Lexplorer.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Diagnostics;
using RestSharp.Serializers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Lexplorer.Services
{
    public interface ILoopringGraphQLService
    {
        void Dispose();
        Task<Account?> GetAccount(string accountId, CancellationToken cancellationToken = default);
        Task<List<AccountTokenBalance>?> GetAccountBalance(string accountId, CancellationToken cancellationToken = default);
        Task<IList<AccountNFTSlot>?> GetAccountNFTs(int skip, int first, string accountId, CancellationToken cancellationToken = default);
        Task<IList<Account>?> GetAccounts(int skip, int first, string? typeName = null, CancellationToken cancellationToken = default);
        Task<IList<Transaction>?> GetAccountTransactions(int skip, int first, string accountId, double? firstBlockId = null, double? lastBlockId = null, CancellationToken cancellationToken = default);
        Task<string?> GetAccountTransactionsResponse(int skip, int first, string accountId, double? firstBlockId = null, double? lastBlockId = null, CancellationToken cancellationToken = default);
        Task<Tuple<double, double>?> GetBlockDateRange(DateTime startDateUTC, DateTime endDateUTC);
        Task<Block?> GetBlockDetails(int blockId, CancellationToken cancellationToken = default);
        Task<Blocks?> GetBlocks(int skip, int first, string orderBy = "internalID", string orderDirection = "desc", string? blockTimestamp = null, bool gte = true);
        Task<NonFungibleToken?> GetNFT(string NFTId, CancellationToken cancellationToken = default);
        Task<IList<NonFungibleToken>?> GetNFTs(int skip = 0, int first = 10, string orderBy = "mintedAt", string orderDirection = "desc");
        Task<IList<Transaction>?> GetNFTTransactions(int skip, int first, string nftId, CancellationToken cancellationToken = default);
        Task<string?> GetNFTTransactionsResponse(int skip, int first, string nftId, CancellationToken cancellationToken = default);
        Task<Pairs?> GetPairs(int skip = 0, int first = 10, string orderBy = "tradedVolumeToken0Swap", string orderDirection = "desc");
        Task<Transaction?> GetTransaction(string transactionId);
        Task<Transactions?> GetTransactions(int skip, int first, string? blockId = null, string? typeName = null, CancellationToken cancellationToken = default);
        Task<IList<object>?> Search(string searchTerm);
    }

    public class LoopringGraphQLService : ILoopringGraphQLService, IDisposable
    {
        const string _baseUrl = "https://api.thegraph.com/subgraphs/name/juanmardefago/loopring36";

        readonly RestClient _client;

        public LoopringGraphQLService()
        {
            _client = new RestClient(_baseUrl);
        }

        public async Task<Blocks?> GetBlocks(int skip, int first, string orderBy = "internalID",
            string orderDirection = "desc", string? blockTimestamp = null, Boolean gte = true)
        {
            var blocksQuery = @"
            query blocks(
                $skip: Int
                $first: Int
                $orderBy: Block_orderBy
                $orderDirection: OrderDirection
                $where: Block_filter
              ) {
                proxy(id: 0) {
                  blockCount
                  userCount
                  transactionCount
                  depositCount
                  withdrawalCount
                  transferCount
                  addCount
                  removeCount
                  orderbookTradeCount
                  swapCount
                  accountUpdateCount
                  ammUpdateCount
                  signatureVerificationCount
                  tradeNFTCount
                  swapNFTCount
                  withdrawalNFTCount
                  transferNFTCount
                  nftMintCount
                  nftDataCount
                }
                blocks(
                  skip: $skip
                  first: $first
                  orderBy: $orderBy
                  orderDirection: $orderDirection
                  where: $where
                ) {
                  ...BlockFragment
                  transactionCount
                }
              }
            "
            + GraphQLFragments.BlockFragment
            + GraphQLFragments.AccountFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            JObject jObject = JObject.FromObject(new
            {
                query = blocksQuery,
                variables = new
                {
                    skip = skip,
                    first = first,
                    orderBy = orderBy,
                    orderDirection = orderDirection,
                    where = new
                    {
                    }
                }
            });
            if (blockTimestamp != null)
            {
                JObject where = (jObject["variables"]!["where"] as JObject)!;
                where.Add(gte
                    ? new JProperty("timestamp_gte", blockTimestamp)
                    : new JProperty("timestamp_lte", blockTimestamp));
            }
            request.AddStringBody(jObject.ToString(), ContentType.Json);
            try
            {
                var response = await _client.PostAsync(request);
                var data = JsonConvert.DeserializeObject<Blocks>(response.Content!);
                return data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<Tuple<Double, Double>?> GetBlockDateRange(DateTime startDateUTC, DateTime endDateUTC)
        {
            var blockQuery = @"
            query swap (
              $timeStampgte: BigInt
              $timeStamplte: BigInt
              )
              {
                firstblock:blocks (first: 1,
                  orderBy: internalID
                  orderDirection: asc
                  where: {
                    timestamp_gte: $timeStampgte
                })
                 {
                  id
                  timestamp
                  }
                lastblock:blocks (first: 1,
                  orderBy: internalID
                  orderDirection: desc
                  where: {
                    timestamp_lte: $timeStamplte
                })
                 {
                  id
                  timestamp
                  }
                }
            ";

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(new
            {
                query = blockQuery,
                variables = new
                {
                    timeStampgte = TimestampConverter.ToTimeStamp(startDateUTC),
                    timeStamplte = TimestampConverter.ToTimeStamp(endDateUTC)
                }
            });
            try
            {
                var response = await _client.PostAsync(request);
                JToken token = JToken.Parse(response.Content!);
                return new Tuple<Double, Double>(
                    (double)token["data"]!["firstblock"]![0]!["id"]!,
                    (double)token["data"]!["lastblock"]![0]!["id"]!);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }

        }

        public async Task<Block?> GetBlockDetails(int blockId, CancellationToken cancellationToken = default)
        {
            var blockQuery = @"
            query block($id: ID!) {
                proxy(id: 0) {
                  blockCount
                }
                block(id: $id) {
                  ...BlockFragment
                  data
                  transactionCount
                  depositCount
                  withdrawalCount
                  transferCount
                  addCount
                  removeCount
                  orderbookTradeCount
                  swapCount
                  accountUpdateCount
                  ammUpdateCount
                  signatureVerificationCount
                  tradeNFTCount
                  swapNFTCount
                  withdrawalNFTCount
                  transferNFTCount
                  nftMintCount
                  nftDataCount
                }
              }
            "
            + GraphQLFragments.BlockFragment
            + GraphQLFragments.AccountFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(new
            {
                query = blockQuery,
                variables = new
                {
                    id = blockId
                }
            });
            var response = await _client.PostAsync(request, cancellationToken);
            var data = JsonConvert.DeserializeObject<Block>(response.Content!);
            return data;
        }

        public async Task<Transaction?> GetTransaction(string transactionId)
        {
            var transactionQuery = @"
              query transaction($id: ID!) {
                transaction(id: $id) {
                  id
                  block {
                    id
                    blockHash
                    timestamp
                    transactionCount
                  }
                  data
                  ...AddFragment
                  ...RemoveFragment
                  ...SwapFragment
                  ...OrderbookTradeFragment
                  ...DepositFragment
                  ...WithdrawalFragment
                  ...TransferFragment
                  ...AccountUpdateFragment
                  ...AmmUpdateFragment
                  ...SignatureVerificationFragment
                  ...TradeNFTFragment
                  ...SwapNFTFragment
                  ...WithdrawalNFTFragment
                  ...TransferNFTFragment
                  ...MintNFTFragment
                  ...DataNFTFragment
                }
              }
            "
              + GraphQLFragments.AccountFragment
              + GraphQLFragments.TokenFragment
              + GraphQLFragments.PoolFragment
              + GraphQLFragments.AccountCreatedAtFragment
              + GraphQLFragments.NFTFragment
              + GraphQLFragments.AddFragment
              + GraphQLFragments.RemoveFragment
              + GraphQLFragments.SwapFragment
              + GraphQLFragments.OrderBookTradeFragment
              + GraphQLFragments.DepositFragment
              + GraphQLFragments.WithdrawalFragment
              + GraphQLFragments.TransferFragment
              + GraphQLFragments.AccountUpdateFragment
              + GraphQLFragments.AmmUpdateFragment
              + GraphQLFragments.SignatureVerificationFragment
              + GraphQLFragments.TradeNFTFragment
              + GraphQLFragments.SwapNFTFragment
              + GraphQLFragments.WithdrawalNFTFragment
              + GraphQLFragments.TransferNFTFragment
              + GraphQLFragments.MintNFTFragment
              + GraphQLFragments.DataNFTFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");

            request.AddJsonBody(new
            {
                query = transactionQuery,
                variables = new
                {
                    id = transactionId
                }
            });

            try
            {
                var response = await _client.PostAsync(request);
                JObject jresponse = JObject.Parse(response.Content!);
                JToken result = jresponse["data"]!["transaction"]!;
                return result.ToObject<Transaction>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<Transactions?> GetTransactions(int skip, int first, string? blockId = null, string? typeName = null, CancellationToken cancellationToken = default)
        {
            var transactionsQuery = @"
              query transactions(
                $skip: Int
                $first: Int
                $orderBy: Transaction_orderBy
                $orderDirection: OrderDirection
                $block: Block_height
                $whereFilter: Transaction_filter
              ) {
                proxy(id: 0) {
                  transactionCount
                  depositCount
                  withdrawalCount
                  transferCount
                  addCount
                  removeCount
                  orderbookTradeCount
                  swapCount
                  accountUpdateCount
                  ammUpdateCount
                  signatureVerificationCount
                  tradeNFTCount
                  swapNFTCount
                  withdrawalNFTCount
                  transferNFTCount
                  nftMintCount
                  nftDataCount
                }
                transactions(
                  skip: $skip
                  first: $first
                  orderBy: $orderBy
                  orderDirection: $orderDirection
                  block: $block
                  where: $whereFilter
                ) {
                  id
                  internalID
                  block {
                    id
                    blockHash
                    timestamp
                    transactionCount
                    depositCount
                    withdrawalCount
                    transferCount
                    addCount
                    removeCount
                    orderbookTradeCount
                    swapCount
                    accountUpdateCount
                    ammUpdateCount
                    signatureVerificationCount
                    tradeNFTCount
                    swapNFTCount
                    withdrawalNFTCount
                    transferNFTCount
                    nftMintCount
                    nftDataCount
                  }
                  data
                  ...AddFragment
                  ...RemoveFragment
                  ...SwapFragment
                  ...OrderbookTradeFragment
                  ...DepositFragment
                  ...WithdrawalFragment
                  ...TransferFragment
                  ...AccountUpdateFragment
                  ...AmmUpdateFragment
                  ...SignatureVerificationFragment
                  ...TradeNFTFragment
                  ...SwapNFTFragment
                  ...WithdrawalNFTFragment
                  ...TransferNFTFragment
                  ...MintNFTFragment
                  ...DataNFTFragment
                }
              }"
              + GraphQLFragments.AccountFragment
              + GraphQLFragments.TokenFragment
              + GraphQLFragments.PoolFragment
              + GraphQLFragments.AccountCreatedAtFragment
              + GraphQLFragments.NFTFragment
              + GraphQLFragments.AddFragment
              + GraphQLFragments.RemoveFragment
              + GraphQLFragments.SwapFragment
              + GraphQLFragments.OrderBookTradeFragment
              + GraphQLFragments.DepositFragment
              + GraphQLFragments.WithdrawalFragment
              + GraphQLFragments.TransferFragment
              + GraphQLFragments.AccountUpdateFragment
              + GraphQLFragments.AmmUpdateFragment
              + GraphQLFragments.SignatureVerificationFragment
              + GraphQLFragments.TradeNFTFragment
              + GraphQLFragments.SwapNFTFragment
              + GraphQLFragments.WithdrawalNFTFragment
              + GraphQLFragments.TransferNFTFragment
              + GraphQLFragments.MintNFTFragment
              + GraphQLFragments.DataNFTFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            if (blockId != null)
            {
                request.AddJsonBody(new
                {
                    query = transactionsQuery,
                    variables = new
                    {
                        skip = skip,
                        first = first,
                        orderBy = "internalID",
                        orderDirection = "desc"
                        ,
                        whereFilter = new
                        {
                            block = blockId
                        }
                    }
                });
            }
            else if (typeName != null)
            {
                request.AddJsonBody(new
                {
                    query = transactionsQuery,
                    variables = new
                    {
                        skip = skip,
                        first = first,
                        orderBy = "internalID",
                        orderDirection = "desc",
                        whereFilter = new
                        {
                            typename = typeName
                        }
                    }
                });
            }
            else
            {
                //remove unused parameter to fix "Invalid query" error with non-hosted graph
                //split and re-join all lines which don't contain the word "whereFilter"
                transactionsQuery = String.Join(Environment.NewLine,
                    transactionsQuery.Split(Environment.NewLine).Where(
                        (a) => !(a.Contains("whereFilter"))));
                request.AddJsonBody(new
                {
                    query = transactionsQuery,
                    variables = new
                    {
                        skip = skip,
                        first = first,
                        orderBy = "internalID",
                        orderDirection = "desc"
                    }
                });
            }

            try
            {
                var response = await _client.PostAsync(request, cancellationToken);
                var data = JsonConvert.DeserializeObject<Transactions>(response.Content!);
                return data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }
        public async Task<IList<Account>?> GetAccounts(int skip, int first, string? typeName = null, CancellationToken cancellationToken = default)
        {
            var accountsQuery = @"
                      query accounts(
                        $skip: Int
                        $first: Int
                        $orderBy: Account_orderBy
                        $orderDirection: OrderDirection
                      ) {
                        accounts(
                          skip: $skip
                          first: $first
                          orderBy: $orderBy
                          orderDirection: $orderDirection
                        ) {
                          ...AccountFragment
                          ...PoolFragment
                          ...UserFragment
                        }
                      }"
                  + GraphQLFragments.AccountFragment
                  + GraphQLFragments.TokenFragment
                  + GraphQLFragments.PoolFragment
                  + GraphQLFragments.UserFragment
                  + GraphQLFragments.AccountCreatedAtFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");

            //since there is no way to filter for typename in Accounts_filter, we simply
            //query a different entity, i.e. pools instead of accounts
            string typePluralName = "accounts";
            if (!String.IsNullOrEmpty(typeName))
            {
                //lower case, but first char only
                typePluralName = Char.ToLower(typeName[0]) + typeName.Substring(1) + "s";
                accountsQuery = accountsQuery.Replace("accounts(", $"{typePluralName}(");
            }

            request.AddJsonBody(new
            {
                query = accountsQuery,
                variables = new
                {
                    skip = skip,
                    first = first,
                    orderBy = "internalID",
                    orderDirection = "desc"
                }
            });

            try
            {
                var response = await _client.PostAsync(request, cancellationToken);
                JObject jresponse = JObject.Parse(response.Content!);
                JToken? token = jresponse["data"]![typePluralName];
                return token!.ToObject<IList<Account>>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }
        public async Task<Account?> GetAccount(string accountId, CancellationToken cancellationToken = default)
        {
            var accountQuery = @"
            query account(
                $accountId: Int
              ) {
                account(
                  id: $accountId
                ) {
                  ...PoolFragment
                  ...UserFragment
                }
            }"
            + GraphQLFragments.PoolFragment
            + GraphQLFragments.UserFragment
            + GraphQLFragments.AccountCreatedAtFragment
            + GraphQLFragments.BlockFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(new
            {
                query = accountQuery,
                variables = new
                {
                    accountId = int.Parse(accountId)
                }
            });
            try
            {
                var response = await _client.PostAsync(request, cancellationToken);
                JObject jresponse = JObject.Parse(response.Content!);
                JToken result = jresponse["data"]!["account"]!;
                return result.ToObject<Account>()!;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }

        }
        public async Task<List<AccountTokenBalance>?> GetAccountBalance(string accountId, CancellationToken cancellationToken = default)
        {
            var balanceQuery = @"
            query accountBalances(
                $accountId: Int
              ) {
                account(
                  id: $accountId
                ) {
                  balances 
                  {
                    id
                    balance
                    token
                    {
                      ...TokenFragment
                    }
                  }
                }
             }
            "
              + GraphQLFragments.TokenFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(new
            {
                query = balanceQuery,
                variables = new
                {
                    accountId = Int32.Parse(accountId)
                }
            });
            var response = await _client.PostAsync(request, cancellationToken);
            try
            {
                JObject jresponse = JObject.Parse(response.Content!);
                IList<JToken> balanceTokens = jresponse["data"]!["account"]!["balances"]!.Children().ToList();
                List<AccountTokenBalance> balances = new List<AccountTokenBalance>();
                foreach (JToken result in balanceTokens)
                {
                    // JToken.ToObject is a helper method that uses JsonSerializer internally
                    AccountTokenBalance balance = result.ToObject<AccountTokenBalance>()!;
                    balances.Add(balance);
                }
                return balances;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<string?> GetAccountTransactionsResponse(int skip, int first, string accountId,
            Double? firstBlockId = null, Double? lastBlockId = null, CancellationToken cancellationToken = default)
        {
            var accountQuery = @"
            query accountTransactions(
                $skip: Int
                $first: Int
                $accountId: Int
                $orderBy: Transaction_orderBy
                $orderDirection: OrderDirection
                $where: Transaction_filter
              ) {
                account(
                  id: $accountId
                ) {
                  transactions(
                    skip: $skip
                    first: $first
                    orderBy: $orderBy
                    orderDirection: $orderDirection
                    where: $where
                  ) {
                    id
                    __typename
                    block {
                      id
                      blockHash
                      timestamp
                      transactionCount
                    }
                    data
                    ...AddFragment
                    ...RemoveFragment
                    ...SwapFragment
                    ...OrderbookTradeFragment
                    ...DepositFragment
                    ...WithdrawalFragment
                    ...TransferFragment
                    ...AccountUpdateFragment
                    ...AmmUpdateFragment
                    ...SignatureVerificationFragment
                    ...TradeNFTFragment
                    ...SwapNFTFragment
                    ...WithdrawalNFTFragment
                    ...TransferNFTFragment
                    ...MintNFTFragment
                    ...DataNFTFragment
                  }
                }
             }
            "
              + GraphQLFragments.AccountFragment
              + GraphQLFragments.TokenFragment
              + GraphQLFragments.PoolFragment
              + GraphQLFragments.AccountCreatedAtFragment
              + GraphQLFragments.NFTFragment
              + GraphQLFragments.AddFragment
              + GraphQLFragments.RemoveFragment
              + GraphQLFragments.SwapFragment
              + GraphQLFragments.OrderBookTradeFragment
              + GraphQLFragments.DepositFragment
              + GraphQLFragments.WithdrawalFragment
              + GraphQLFragments.TransferFragment
              + GraphQLFragments.AccountUpdateFragment
              + GraphQLFragments.AmmUpdateFragment
              + GraphQLFragments.SignatureVerificationFragment
              + GraphQLFragments.TradeNFTFragment
              + GraphQLFragments.SwapNFTFragment
              + GraphQLFragments.WithdrawalNFTFragment
              + GraphQLFragments.TransferNFTFragment
              + GraphQLFragments.MintNFTFragment
              + GraphQLFragments.DataNFTFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            JObject jObject = JObject.FromObject(new
            {
                query = accountQuery,
                variables = new
                {
                    skip = skip,
                    first = first,
                    accountId = int.Parse(accountId),
                    orderBy = "internalID",
                    orderDirection = "desc",
                    where = new
                    {
                    },
                }
            });
            if (firstBlockId.HasValue && lastBlockId.HasValue)
            {
                JObject where = (jObject["variables"]!["where"] as JObject)!;
                where.Add(new JProperty("block_gte", firstBlockId.ToString()));
                where.Add(new JProperty("block_lte", lastBlockId.ToString()));
            }
            request.AddStringBody(jObject.ToString(), ContentType.Json);
            try
            {
                var response = await _client.PostAsync(request, cancellationToken);
                return response.Content;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<IList<Transaction>?> GetAccountTransactions(int skip, int first, string accountId,
            Double? firstBlockId = null, Double? lastBlockId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                string? response = await GetAccountTransactionsResponse(skip, first, accountId, firstBlockId, lastBlockId, cancellationToken);
                JObject jresponse = JObject.Parse(response!);
                JToken? token = jresponse["data"]!["account"]!["transactions"];
                return token!.ToObject<IList<Transaction>>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return new List<Transaction>();
            }
        }

        public async Task<IList<AccountNFTSlot>?> GetAccountNFTs(int skip, int first, string accountId, CancellationToken cancellationToken = default)
        {
            var accountNFTQuery = @"
            query accountNFTSlotsQuery(
                $skip: Int
                $first: Int
                $orderBy: AccountNFTSlot_orderBy
                $orderDirection: OrderDirection
                $where: AccountNFTSlot_filter
              ) {
                accountNFTSlots(
                    skip: $skip
                    first: $first
                    orderBy: $orderBy
                    orderDirection: $orderDirection
                    where: $where
                  ) {
                    id
                    __typename
                    balance
                    nft {
                      id
                      ...NFTFragment
                    }
                }
             }
            "
              + GraphQLFragments.AccountFragment
              + GraphQLFragments.TokenFragment
              + GraphQLFragments.NFTFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            JObject jObject = JObject.FromObject(new
            {
                query = accountNFTQuery,
                variables = new
                {
                    skip = skip,
                    first = first,
                    orderBy = "lastUpdatedAt",
                    orderDirection = "desc",
                    where = new
                    {
                        account = accountId,
                        nft_not = ""
                    },
                }
            });
            request.AddStringBody(jObject.ToString(), ContentType.Json);
            try
            {
                var response = await _client.PostAsync(request, cancellationToken);
                JObject jresponse = JObject.Parse(response.Content!);
                JToken? token = jresponse["data"]!["accountNFTSlots"];
                return token!.ToObject<IList<AccountNFTSlot>>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<Pairs?> GetPairs(int skip = 0, int first = 10, string orderBy = "tradedVolumeToken0Swap", string orderDirection = "desc")
        {
            var pairsQuery = @"
             query pairs(
                $skip: Int
                $first: Int
                $orderBy: Pair_orderBy
                $orderDirection: OrderDirection
              ) {
                pairs(
                  skip: $skip
                  first: $first
                  orderBy: $orderBy
                  orderDirection: $orderDirection
                ) {
                  id
                  internalID
                  token0 {
                    ...TokenFragment
                  }
                  token1 {
                    ...TokenFragment
                  }
                  dailyEntities(skip: 1, first: 1, orderBy: dayEnd, orderDirection: desc) {
                    tradedVolumeToken1Swap
                    tradedVolumeToken0Swap
                    id
                  }
                  weeklyEntities(
                    skip: 0
                    first: 1
                    orderBy: weekEnd
                    orderDirection: desc
                  ) {
                    tradedVolumeToken1Swap
                    tradedVolumeToken0Swap
                    id
                  }
                }
              }
            "
              + GraphQLFragments.TokenFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(new
            {
                query = pairsQuery,
                variables = new
                {
                    skip = skip,
                    first = first,
                    orderBy = orderBy,
                    orderDirection = orderDirection
                }
            });
            try
            {
                var response = await _client.PostAsync(request);
                var data = JsonConvert.DeserializeObject<Pairs>(response.Content!);
                return data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        private static readonly Dictionary<string, Type> resultCategories
            = new Dictionary<string, Type>
        {
                { "accounts", typeof(Account) },
                { "accountsByAddress", typeof(Account) },
                { "blocks", typeof(BlockDetail) },
                { "transactions", typeof(Transaction) },
                { "nonFungibleTokens", typeof(NonFungibleToken) },
                { "nonFungibleTokensBynftID", typeof(NonFungibleToken) },
        };
        public async Task<IList<object>?> Search(string searchTerm)
        {
            var searchQuery = @"
             query search(
                $searchTerm: String
                $searchTermBytes: Bytes
              ) {
                accounts(
                  where: {id: $searchTerm}
                ) {
                  id
                  __typename
                }
                accountsByAddress: accounts(
                  where: {address: $searchTermBytes}
                ) {
                  id
                  address
                  __typename
                }
                blocks(
                  where: {id: $searchTerm}
                ) {
                  id
                  __typename
                }
                transactions(
                  where: {id: $searchTerm}
                ) {
                  id
                  __typename
                }
                nonFungibleTokens(
                  where: {id: $searchTerm}
                ) {
                  id
                  __typename
                  nftID
                }
                nonFungibleTokensBynftID: nonFungibleTokens(
                  where: {nftID: $searchTermBytes}
                ) {
                  id
                  __typename
                  nftID
                }
              }
            ";

            //avoid query errors with search strings that cannot be converted to bytes
            //extra searchTermBytes is only filled if it matches strict RegEx, starting with 0x (added if missing)
            //and then any number of pairs "({2})+" of 0-9, a-f, A-F, end must be reached = $
            string searchTermBytes = (searchTerm.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) ? searchTerm : "0x" + searchTerm).ToLower();
            if (!Regex.Match(searchTermBytes, "0x([a-f0-9]{2})+$").Success)
                searchTermBytes = "";
            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(new
            {
                query = searchQuery,
                variables = new
                {
                    searchTerm = searchTerm,
                    searchTermBytes = searchTermBytes
                }
            });
            try
            {
                var response = await _client.PostAsync(request);
                List<object> results = new List<object>();
                JObject responseData = JObject.Parse(response.Content!);
                foreach (var item in resultCategories)
                {
                    JToken? token = responseData["data"]?[item.Key];
                    if (token == null) continue;
                    IList<JToken> resultsPerCategory = token.Children().ToList();
                    foreach (JToken result in resultsPerCategory)
                    {
                        results.Add(result.ToObject(item.Value)!);
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<IList<NonFungibleToken>?> GetNFTs(int skip = 0, int first = 10, string orderBy = "mintedAt", string orderDirection = "desc")
        {
            var nonFungibleTokensQuery = @"
             query nonFungibleTokensQuery(
                $skip: Int
                $first: Int
                $orderBy: NonFungibleToken_orderBy
                $orderDirection: OrderDirection
              ) {
                nonFungibleTokens(
                  skip: $skip
                  first: $first
                  orderBy: $orderBy
                  orderDirection: $orderDirection
                ) {
                  ...NFTFragment
                  mintedAtTransaction {
                    ...MintNFTFragmentWithoutNFT
                  }
                }
              }
            "
              + GraphQLFragments.NFTFragment
              + GraphQLFragments.MintNFTFragmentWithoutNFT
              + GraphQLFragments.AccountFragment
              + GraphQLFragments.TokenFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(new
            {
                query = nonFungibleTokensQuery,
                variables = new
                {
                    skip = skip,
                    first = first,
                    orderBy = orderBy,
                    orderDirection = orderDirection
                }
            });
            try
            {
                var response = await _client.PostAsync(request);
                JObject jresponse = JObject.Parse(response.Content!);
                JToken? token = jresponse["data"]!["nonFungibleTokens"];
                return token!.ToObject<IList<NonFungibleToken>>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<NonFungibleToken?> GetNFT(string NFTId, CancellationToken cancellationToken = default)
        {
            var NFTQuery = @"
              query nonFungibleTokenQuery($id: ID!) {
                nonFungibleToken(id: $id) {
                  id
                  mintedAtTransaction {
                    ...MintNFTFragmentWithoutNFT
                  }
                  ...NFTFragment
                }
              }
            "
              + GraphQLFragments.AccountFragment
              + GraphQLFragments.NFTFragment
              + GraphQLFragments.MintNFTFragmentWithoutNFT
              + GraphQLFragments.TokenFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");

            request.AddJsonBody(new
            {
                query = NFTQuery,
                variables = new
                {
                    id = NFTId
                }
            });

            try
            {
                var response = await _client.PostAsync(request, cancellationToken);
                JObject jresponse = JObject.Parse(response.Content!);
                JToken result = jresponse["data"]!["nonFungibleToken"]!;
                return result.ToObject<NonFungibleToken>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<string?> GetNFTTransactionsResponse(int skip, int first, string nftId, CancellationToken cancellationToken = default)
        {
            var nftQuery = @"
            query nftTransactions(
                $skip: Int
                $first: Int
                $nftId: String
                $orderBy: Transaction_orderBy
                $orderDirection: OrderDirection
              ) {
                nonFungibleToken(
                  id: $nftId
                ) {
                  transactions(
                    skip: $skip
                    first: $first
                    orderBy: $orderBy
                    orderDirection: $orderDirection
                  ) {
                    id
                    __typename
                    block {
                        id
                        timestamp
                    }
                    ...TradeNFTFragment
                    ...SwapNFTFragment
                    ...WithdrawalNFTFragment
                    ...TransferNFTFragment
                    ...MintNFTFragmentWithoutNFT
                    ...DataNFTFragment
                  }
                }
             }
            "
              + GraphQLFragments.AccountFragment
              + GraphQLFragments.TokenFragment
              + GraphQLFragments.NFTFragment
              + GraphQLFragments.TradeNFTFragment
              + GraphQLFragments.SwapNFTFragment
              + GraphQLFragments.WithdrawalNFTFragment
              + GraphQLFragments.TransferNFTFragment
              + GraphQLFragments.MintNFTFragmentWithoutNFT
              + GraphQLFragments.DataNFTFragment;

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            JObject jObject = JObject.FromObject(new
            {
                query = nftQuery,
                variables = new
                {
                    skip = skip,
                    first = first,
                    nftId = nftId,
                    orderBy = "internalID",
                    orderDirection = "desc"
                }
            });
            request.AddStringBody(jObject.ToString(), ContentType.Json);
            try
            {
                var response = await _client.PostAsync(request, cancellationToken);
                return response.Content;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<IList<Transaction>?> GetNFTTransactions(int skip, int first, string nftId, CancellationToken cancellationToken = default)
        {
            try
            {
                string? response = await GetNFTTransactionsResponse(skip, first, nftId, cancellationToken);
                JObject jresponse = JObject.Parse(response!);
                JToken? token = jresponse["data"]!["nonFungibleToken"]!["transactions"];
                return token!.ToObject<IList<Transaction>>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return new List<Transaction>();
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
