using System.Text.Json.Nodes;
using crypto_bot_api.Models;
using crypto_bot_api.Models.DTOs.Orders;
using crypto_bot_api.CustomExceptions;

namespace crypto_bot_api.Services
{
    public interface IOrderMonitoringService
    {
        Task<FinalizedOrderDetails> MonitorOrderAsync(string orderId, CancellationToken cancellationToken = default);
    }

    public class OrderMonitoringService : IOrderMonitoringService
    {
        private readonly ICoinbaseOrderApiClient _orderApiClient;
        private readonly TimeSpan _checkInterval;
        private static readonly string[] TerminalStates = { "CANCELLED", "REJECTED", "EXPIRED" };
        private static readonly string[] StatesWithPossibleFills = { "CANCELLED", "EXPIRED" };

        public OrderMonitoringService(
            ICoinbaseOrderApiClient orderApiClient,
            TimeSpan? checkInterval = null)
        {
            _orderApiClient = orderApiClient;
            _checkInterval = checkInterval ?? TimeSpan.FromSeconds(10);
        }

        public async Task<FinalizedOrderDetails> MonitorOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                var terminalState = await WaitForOrderToDisappearAsync(orderId, cancellationToken);
                var finalDetails = new FinalizedOrderDetails { OrderId = orderId };
                
                if (!string.IsNullOrEmpty(terminalState))
                {
                    finalDetails.Status = terminalState;
                    
                    // Only check for fills if the terminal state is one that could have fills
                    if (StatesWithPossibleFills.Contains(terminalState))
                    {
                        finalDetails = await GetFinalOrderDetailsAsync(orderId, cancellationToken);
                        finalDetails.Status = terminalState; // Preserve the terminal state
                    }
                }
                else
                {
                    finalDetails = await GetFinalOrderDetailsAsync(orderId, cancellationToken);
                }
                
                return finalDetails;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new CoinbaseApiException($"Error monitoring order {orderId}: {ex.Message}");
            }
        }

        private async Task<string> WaitForOrderToDisappearAsync(string orderId, CancellationToken cancellationToken)
        {
            while (true)
            {
                // Check cancellation before making the API call
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var request = new ListOrdersRequestDto
                    {
                        OrderIds = new[] { orderId }
                    };

                    var response = await _orderApiClient.ListOrdersAsync(request);
                    var orders = response["orders"]?.AsArray();

                    if (orders == null || orders.Count == 0)
                    {
                        return string.Empty;
                    }

                    var order = orders[0];
                    var status = order?["status"]?.GetValue<string>();
                    if (status != null && TerminalStates.Contains(status))
                    {
                        return status;
                    }

                    await Task.Delay(_checkInterval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }

        private async Task<FinalizedOrderDetails> GetFinalOrderDetailsAsync(string orderId, CancellationToken cancellationToken)
        {
            var finalOrderDetails = new FinalizedOrderDetails
            {
                OrderId = orderId,
                TotalCommission = 0
            };

            var fillsRequest = new ListOrderFillsRequestDto
            {
                OrderId = orderId
            };

            var fillsResponse = await _orderApiClient.ListOrderFillsAsync(fillsRequest);
            var fills = fillsResponse["fills"]?.AsArray();

            if (fills != null && fills.Count > 0)
            {
                var latestFill = fills[0]?.AsObject();
                if (latestFill != null)
                {
                    finalOrderDetails.EntryId = latestFill["entry_id"]?.GetValue<string>() ?? string.Empty;
                    finalOrderDetails.TradeId = latestFill["trade_id"]?.GetValue<string>() ?? string.Empty;
                    finalOrderDetails.TradeTime = DateTime.Parse(latestFill["trade_time"]?.GetValue<string>() ?? DateTime.UtcNow.ToString());
                    finalOrderDetails.TradeType = latestFill["trade_type"]?.GetValue<string>() ?? string.Empty;
                    finalOrderDetails.Price = decimal.Parse(latestFill["price"]?.GetValue<string>() ?? "0");
                    finalOrderDetails.Size = decimal.Parse(latestFill["size"]?.GetValue<string>() ?? "0");
                    finalOrderDetails.ProductId = latestFill["product_id"]?.GetValue<string>() ?? string.Empty;
                    finalOrderDetails.SequenceTimestamp = DateTime.Parse(latestFill["sequence_timestamp"]?.GetValue<string>() ?? DateTime.UtcNow.ToString());
                    finalOrderDetails.LiquidityIndicator = latestFill["liquidity_indicator"]?.GetValue<string>() ?? string.Empty;
                    finalOrderDetails.SizeInQuote = latestFill["size_in_quote"]?.GetValue<bool>() ?? false;
                    finalOrderDetails.UserId = latestFill["user_id"]?.GetValue<string>() ?? string.Empty;
                    finalOrderDetails.Side = latestFill["side"]?.GetValue<string>() ?? string.Empty;
                    finalOrderDetails.RetailPortfolioId = latestFill["retail_portfolio_id"]?.GetValue<string>() ?? string.Empty;
                }

                foreach (var fill in fills)
                {
                    if (fill == null) continue;

                    var commission = fill["commission"]?.GetValue<string>();
                    if (decimal.TryParse(commission, out decimal fillCommission))
                    {
                        finalOrderDetails.TotalCommission += fillCommission;
                    }

                    var tradeType = fill["trade_type"]?.GetValue<string>();
                    if (tradeType == "FILL")
                    {
                        finalOrderDetails.Status = "FILLED";
                    }
                }
            }

            return finalOrderDetails;
        }
    }
} 