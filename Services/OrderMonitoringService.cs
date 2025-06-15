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
        private readonly IAssembleOrderDetailsService _assembleOrderDetailsService;
        private readonly TimeSpan _pollingInterval;
        private readonly TimeSpan _defaultTimeout;
        private readonly int _maxAttempts = 1000; // Safety limit for number of polling attempts

        public OrderMonitoringService(
            ICoinbaseOrderApiClient orderApiClient,
            IAssembleOrderDetailsService assembleOrderDetailsService,
            TimeSpan pollingInterval,
            TimeSpan? defaultTimeout = null)
        {
            _orderApiClient = orderApiClient;
            _assembleOrderDetailsService = assembleOrderDetailsService;
            _pollingInterval = pollingInterval;
            _defaultTimeout = defaultTimeout ?? TimeSpan.FromMinutes(30);
        }

        public async Task<FinalizedOrderDetails> MonitorOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Calculate timeout based on default or GTD order end time
                TimeSpan timeout = _defaultTimeout;
                JsonObject? order = null;

                try
                {
                    // First check to get initial state
                    var orderResponse = await _orderApiClient.ListOrdersAsync(
                        new ListOrdersRequestDto { OrderIds = new[] { orderId } });

                    cancellationToken.ThrowIfCancellationRequested();
                    order = (orderResponse["orders"] as JsonArray)?.FirstOrDefault()?.AsObject();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new CoinbaseApiException($"Error retrieving order {orderId}: {ex.Message}", ex);
                }

                if (order == null)
                {
                    throw new CoinbaseApiException($"Order {orderId} not found");
                }

                // Handle GTD orders
                if (order["end_time"] != null && DateTime.TryParse(order["end_time"]?.ToString(), out var endTime))
                {
                    if (DateTime.UtcNow >= endTime)
                    {
                        throw new TimeoutException($"GTD order {orderId} has already expired at {endTime}");
                    }
                    
                    var gtdTimeout = endTime - DateTime.UtcNow;
                    if (gtdTimeout < timeout)
                    {
                        timeout = gtdTimeout;
                    }
                }

                using var timeoutCts = new CancellationTokenSource(timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                decimal? initialSize = null;
                if (order["size"] != null && decimal.TryParse(order["size"]?.ToString(), out var size))
                {
                    initialSize = size;
                }

                var status = order["status"]?.ToString();
                if (IsTerminalState(status))
                {
                    // Only check fills for FILLED state or if there might be partial fills
                    if (status == "FILLED" || status == "CANCELLED" || status == "EXPIRED")
                    {
                        var fills = await GetOrderFills(orderId);
                        return _assembleOrderDetailsService.AssembleFromFills(orderId, fills, status, initialSize);
                    }
                    
                    // For REJECTED orders, don't check fills
                    return _assembleOrderDetailsService.AssembleFromFills(orderId, new JsonArray(), status, initialSize);
                }

                int attempts = 0;
                while (!linkedCts.Token.IsCancellationRequested && attempts < _maxAttempts)
                {
                    attempts++;

                    try
                    {
                        await Task.Delay(_pollingInterval, linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Check which token triggered the cancellation
                        if (timeoutCts.Token.IsCancellationRequested)
                        {
                            throw new TimeoutException($"Order {orderId} monitoring exceeded timeout of {timeout.TotalMinutes} minutes");
                        }
                        throw; // User cancellation
                    }

                    try
                    {
                        var orderResponse = await _orderApiClient.ListOrdersAsync(
                            new ListOrdersRequestDto { OrderIds = new[] { orderId } });

                        // Check for cancellation after API call
                        if (timeoutCts.Token.IsCancellationRequested)
                        {
                            throw new TimeoutException($"Order {orderId} monitoring exceeded timeout of {timeout.TotalMinutes} minutes");
                        }
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }

                        order = (orderResponse["orders"] as JsonArray)?.FirstOrDefault()?.AsObject();
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    if (order == null)
                    {
                        // Order not found, check fills to determine final state
                        var fills = await GetOrderFills(orderId);
                        return _assembleOrderDetailsService.AssembleFromFills(orderId, fills, "FILLED", initialSize);
                    }

                    status = order["status"]?.ToString();
                    if (IsTerminalState(status))
                    {
                        // Only check fills for FILLED state or if there might be partial fills
                        if (status == "FILLED" || status == "CANCELLED" || status == "EXPIRED")
                        {
                            var fills = await GetOrderFills(orderId);
                            return _assembleOrderDetailsService.AssembleFromFills(orderId, fills, status, initialSize);
                        }
                        
                        // For REJECTED orders, don't check fills
                        return _assembleOrderDetailsService.AssembleFromFills(orderId, new JsonArray(), status, initialSize);
                    }
                }

                if (attempts >= _maxAttempts)
                {
                    throw new TimeoutException($"Order {orderId} monitoring exceeded maximum number of attempts ({_maxAttempts})");
                }

                throw new TimeoutException($"Order {orderId} monitoring exceeded timeout of {timeout.TotalMinutes} minutes");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TimeoutException)
            {
                throw new CoinbaseApiException($"Error monitoring order {orderId}: {ex.Message}", ex);
            }
        }

        private async Task<JsonArray> GetOrderFills(string orderId)
        {
            var fillsResponse = await _orderApiClient.ListOrderFillsAsync(
                new ListOrderFillsRequestDto { OrderId = orderId });

            return fillsResponse["fills"] as JsonArray ?? new JsonArray();
        }

        private static bool IsTerminalState(string? status)
        {
            return status is "FILLED" or "CANCELLED" or "EXPIRED" or "REJECTED";
        }
    }
} 