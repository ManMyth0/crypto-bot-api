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
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

        public OrderMonitoringService(ICoinbaseOrderApiClient orderApiClient)
        {
            _orderApiClient = orderApiClient;
        }

        public async Task<FinalizedOrderDetails> MonitorOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Step 1: Monitor orders list until order disappears
                await WaitForOrderToDisappearAsync(orderId, cancellationToken);

                // Step 2: Get final order status and details
                return await GetFinalOrderDetailsAsync(orderId, cancellationToken);
            }
            catch (Exception ex) when (ex is not CoinbaseApiException && ex is not OperationCanceledException)
            {
                throw new CoinbaseApiException($"Error monitoring order {orderId}: {ex.Message}");
            }
        }

        private async Task WaitForOrderToDisappearAsync(string orderId, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var request = new ListOrdersRequestDto
                {
                    OrderIds = new[] { orderId }
                };

                var response = await _orderApiClient.ListOrdersAsync(request);
                var orders = response["orders"]?.AsArray();

                if (orders == null || orders.Count == 0)
                {
                    return;
                }

                await Task.Delay(CheckInterval, cancellationToken);
            }
        }

        private async Task<FinalizedOrderDetails> GetFinalOrderDetailsAsync(
            string orderId, CancellationToken cancellationToken)
        {
            var finalOrderDetails = new FinalizedOrderDetails
            {
                OrderId = orderId,
                TotalCommission = 0
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                var fillsRequest = new ListOrderFillsRequestDto
                {
                    OrderId = orderId
                };

                var fillsResponse = await _orderApiClient.ListOrderFillsAsync(fillsRequest);
                var fills = fillsResponse["fills"]?.AsArray();

                if (fills != null && fills.Count > 0)
                {
                    // Take the most recent fill for most of the details
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

                    // Sum up all commissions
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

                    if (finalOrderDetails.Status == "FILLED")
                    {
                        return finalOrderDetails;
                    }
                }

                // Check if order was cancelled or rejected
                var ordersRequest = new ListOrdersRequestDto
                {
                    OrderIds = new[] { orderId }
                };

                var ordersResponse = await _orderApiClient.ListOrdersAsync(ordersRequest);
                var orders = ordersResponse["orders"]?.AsArray();

                if (orders != null && orders.Count > 0)
                {
                    var order = orders[0];
                    var status = order?["status"]?.GetValue<string>();

                    if (status == "CANCELLED" || status == "REJECTED" || status == "EXPIRED")
                    {
                        finalOrderDetails.Status = status;
                        return finalOrderDetails;
                    }
                }

                await Task.Delay(CheckInterval, cancellationToken);
            }

            throw new OperationCanceledException("Order monitoring was cancelled");
        }
    }
} 