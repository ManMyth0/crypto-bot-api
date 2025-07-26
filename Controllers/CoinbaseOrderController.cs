using System.Text.Json.Nodes;
using crypto_bot_api.Services;
using Microsoft.AspNetCore.Mvc;
using crypto_bot_api.Utilities;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;

namespace crypto_bot_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoinbaseOrderController : ControllerBase
    {
        private readonly ICoinbaseOrderApiClient _coinbaseOrderClient;
        private readonly IAssembleOrderDetailsService _assembleOrderDetails;
        private readonly IPositionManagementService _positionManager;
        private readonly IOrderValidationService _orderValidation;
        private readonly IOrderMonitoringService _orderMonitoringService;
        private readonly ILogger<CoinbaseOrderController> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        
        public CoinbaseOrderController(
            ICoinbaseOrderApiClient coinbaseOrderClient,
            IAssembleOrderDetailsService assembleOrderDetails,
            IPositionManagementService positionManager,
            IOrderValidationService orderValidation,
            IOrderMonitoringService orderMonitoringService,
            ILogger<CoinbaseOrderController> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _coinbaseOrderClient = coinbaseOrderClient;
            _assembleOrderDetails = assembleOrderDetails;
            _positionManager = positionManager;
            _orderValidation = orderValidation;
            _orderMonitoringService = orderMonitoringService;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        [HttpPost("orders")]
        public async Task<ActionResult<OrderResponse>> CreateOrder(CreateOrderRequestDto orderRequest)
        {
            try
            {
                _logger.LogInformation("=== ORDER CREATION STARTED ===");
                
                // Generate client order ID if not provided
                if (string.IsNullOrEmpty(orderRequest.ClientOrderId))
                {
                    orderRequest.ClientOrderId = ClientOrderIdGenerator.GenerateCoinbaseClientOrderId();
                    _logger.LogInformation("Generated client order ID: {ClientOrderId}", orderRequest.ClientOrderId);
                }
                else
                {
                    _logger.LogInformation("Using provided client order ID: {ClientOrderId}", orderRequest.ClientOrderId);
                }

                // Validate the order
                _logger.LogInformation("Starting order validation...");
                var validation = await _orderValidation.ValidateOrderAsync(orderRequest);
                _logger.LogInformation("Order validation completed. IsValid: {IsValid}, Warnings: {WarningCount}", 
                    validation.IsValid, validation.Warnings?.Count ?? 0);

                // If validation fails, return error response
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Order validation failed. Returning BadRequest.");
                    return BadRequest(new 
                    { 
                        Error = "Order validation failed",
                        ValidationErrors = validation.Warnings,
                        ValidationResult = validation
                    });
                }

                // Create order only if validation passes
                _logger.LogInformation("Order validation passed. Creating order on Coinbase...");
                var result = await _coinbaseOrderClient.CreateOrderAsync(orderRequest);
                _logger.LogInformation("Order creation on Coinbase completed. Response received.");
                
                // Log the full response structure to understand the format
                _logger.LogInformation("Full Coinbase response: {Response}", result?.ToJsonString() ?? "NULL");

                // Extract order ID from the response
                var orderId = result["order_id"]?.ToString();
                _logger.LogInformation("Extracted order ID from response: {OrderId}", orderId ?? "NULL");
                
                // Also try alternative paths in case the structure is different
                if (string.IsNullOrEmpty(orderId))
                {
                    _logger.LogInformation("Trying alternative response paths...");
                    var successResponse = result?["success_response"];
                    if (successResponse != null)
                    {
                        orderId = successResponse["order_id"]?.ToString();
                        _logger.LogInformation("Extracted order ID from success_response: {OrderId}", orderId ?? "NULL");
                    }
                    
                    if (string.IsNullOrEmpty(orderId))
                    {
                        // Try to find order_id anywhere in the response
                        var allKeys = GetAllKeys(result);
                        _logger.LogInformation("All keys in response: {Keys}", string.Join(", ", allKeys));
                    }
                }

                if (!string.IsNullOrEmpty(orderId))
                {
                    _logger.LogInformation("=== STARTING BACKGROUND MONITORING ===");
                    _logger.LogInformation("Order {OrderId} created successfully, starting monitoring in background", orderId);
                    
                    // Start monitoring in background (fire and forget)
                    _ = Task.Run(async () =>
                    {
                        _logger.LogInformation("=== BACKGROUND MONITORING TASK STARTED ===");
                        
                        // Create a new service scope for the background task
                        using var scope = _serviceScopeFactory.CreateScope();
                        var orderMonitoringService = scope.ServiceProvider.GetRequiredService<IOrderMonitoringService>();
                        var positionManager = scope.ServiceProvider.GetRequiredService<IPositionManagementService>();
                        
                        try
                        {
                            _logger.LogInformation("Calling _orderMonitoringService.MonitorOrderAsync for order {OrderId}", orderId);
                            var finalizedDetails = await orderMonitoringService.MonitorOrderAsync(orderId);
                            _logger.LogInformation("MonitorOrderAsync completed for order {OrderId}. Result: {Result}", 
                                orderId, finalizedDetails != null ? "NOT NULL" : "NULL");
                            
                            if (finalizedDetails != null)
                            {
                                _logger.LogInformation("Order {OrderId} completed with status: {Status}", orderId, finalizedDetails.Status);
                                
                                // Handle position management based on order status and type
                                if (finalizedDetails.Status == "FILLED" && !string.IsNullOrEmpty(finalizedDetails.Trade_Type))
                                {
                                    _logger.LogInformation("Order {OrderId} is FILLED with Trade_Type: {TradeType}", orderId, finalizedDetails.Trade_Type);
                                    try
                                    {
                                        if (finalizedDetails.Trade_Type == "BUY")
                                        {
                                            _logger.LogInformation("Processing BUY order {OrderId} - creating new LONG position", orderId);
                                            // Create new LONG position for BUY orders
                                            var position = await positionManager.CreatePositionFromOrderAsync(finalizedDetails, "LONG");
                                            _logger.LogInformation("Created new LONG position {PositionId} for order {OrderId}", position.position_uuid, orderId);
                                        }
                                        else if (finalizedDetails.Trade_Type == "SELL")
                                        {
                                            // Determine if this is closing a position (OFFLOAD) or opening a new SHORT position
                                            var originalPositionType = orderRequest.PositionType?.ToUpperInvariant();
                                            
                                            if (originalPositionType == "OFFLOAD")
                                            {
                                                _logger.LogInformation("Processing SELL order {OrderId} with OFFLOAD - finding and closing position", orderId);
                                                try
                                                {
                                                    // Automatically find and close the appropriate position
                                                    var position = await positionManager.UpdatePositionFromClosingOrderAsync(finalizedDetails);
                                                    _logger.LogInformation("Updated position {PositionId} for order {OrderId}", position.position_uuid, orderId);
                                                }
                                                catch (InvalidOperationException ex)
                                                {
                                                    _logger.LogWarning("SELL order {OrderId} completed but no open position found to close: {Message}", orderId, ex.Message);
                                                }
                                            }
                                            else if (originalPositionType == "SHORT")
                                            {
                                                _logger.LogInformation("Processing SELL order {OrderId} - creating new SHORT position", orderId);
                                                // Create new SHORT position for SELL orders
                                                var position = await positionManager.CreatePositionFromOrderAsync(finalizedDetails, "SHORT");
                                                _logger.LogInformation("Created new SHORT position {PositionId} for order {OrderId}", position.position_uuid, orderId);
                                            }
                                            else
                                            {
                                                _logger.LogWarning("SELL order {OrderId} with unknown position type: {PositionType}", orderId, originalPositionType);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error managing position for order {OrderId}", orderId);
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation("Order {OrderId} completed with status {Status}, no position management needed", orderId, finalizedDetails.Status);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("MonitorOrderAsync returned null for order {OrderId}", orderId);
                            }
                        }
                        catch (TimeoutException ex)
                        {
                            _logger.LogWarning("Order {OrderId} monitoring timed out: {Message}", orderId, ex.Message);
                        }
                        catch (CoinbaseApiException ex)
                        {
                            _logger.LogError(ex, "Coinbase API error monitoring order {OrderId}", orderId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error monitoring order {OrderId}", orderId);
                        }
                        
                        _logger.LogInformation("=== BACKGROUND MONITORING TASK COMPLETED ===");
                    });
                    
                    _logger.LogInformation("Background monitoring task started for order {OrderId}", orderId);
                }
                else
                {
                    _logger.LogWarning("No order ID found in response. Cannot start monitoring.");
                }

                _logger.LogInformation("=== RETURNING ORDER RESPONSE ===");
                // Return order result immediately
                var orderResponse = new OrderResponse
                {
                    Order = result,
                    ValidationResult = validation,
                    PositionType = orderRequest.PositionType,
                    ClientOrderId = orderRequest.ClientOrderId
                };
                
                _logger.LogInformation("Returning order response: {Response}", orderResponse.Order?.ToJsonString() ?? "NULL");
                return Ok(orderResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        private List<string> GetAllKeys(JsonNode? node, string prefix = "")
        {
            var keys = new List<string>();
            
            if (node == null) return keys;
            
            if (node is JsonObject obj)
            {
                foreach (var kvp in obj)
                {
                    var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
                    keys.Add(key);
                    keys.AddRange(GetAllKeys(kvp.Value, key));
                }
            }
            else if (node is JsonArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    var key = $"{prefix}[{i}]";
                    keys.AddRange(GetAllKeys(arr[i], key));
                }
            }
            
            return keys;
        }

        [HttpGet("historical/fills")]
        public async Task<ActionResult<JsonObject>> GetOrderFills(
            [FromQuery] string? orderId = null,
            [FromQuery(Name = "order_ids")] string[]? orderIds = null,
            [FromQuery(Name = "trade_ids")] string[]? tradeIds = null,
            [FromQuery] string? productId = null,
            [FromQuery(Name = "product_ids")] string[]? productIds = null,
            [FromQuery] string? startSequenceTimestamp = null,
            [FromQuery] string? endSequenceTimestamp = null,
            [FromQuery] int? limit = 50,
            [FromQuery] string? cursor = null,
            [FromQuery] string? sortBy = null)
        {
            try
            {
                var fillsRequest = new ListOrderFillsRequestDto
                {
                    OrderId = orderId,
                    OrderIds = orderIds,
                    TradeIds = tradeIds,
                    ProductId = productId,
                    ProductIds = productIds,
                    StartSequenceTimestamp = startSequenceTimestamp,
                    EndSequenceTimestamp = endSequenceTimestamp,
                    Limit = limit,
                    Cursor = cursor,
                    SortBy = sortBy
                };

                var result = await _coinbaseOrderClient.ListOrderFillsAsync(fillsRequest);
                return result;
            }
            catch (CoinbaseApiException ex)
            {
                return StatusCode(502, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("historical/batch")]
        public async Task<ActionResult<JsonObject>> GetOrders(
            [FromQuery(Name = "order_ids")] string[]? orderIds = null,
            [FromQuery(Name = "product_ids")] string[]? productIds = null,
            [FromQuery] string? productType = null,
            [FromQuery(Name = "order_status")] string[]? orderStatus = null,
            [FromQuery(Name = "time_in_forces")] string[]? timeInForces = null,
            [FromQuery(Name = "order_types")] string[]? orderTypes = null,
            [FromQuery] string? orderSide = null,
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null,
            [FromQuery] string? orderPlacementSource = null,
            [FromQuery] string? contractExpiryType = null,
            [FromQuery(Name = "asset_filters")] string[]? assetFilters = null,
            [FromQuery] string? retailPortfolioId = null,
            [FromQuery] int? limit = 50,
            [FromQuery] string? cursor = null,
            [FromQuery] string? sortBy = null)
        {
            try
            {
                var ordersRequest = new ListOrdersRequestDto
                {
                    OrderIds = orderIds,
                    ProductIds = productIds,
                    ProductType = productType,
                    OrderStatus = orderStatus,
                    TimeInForces = timeInForces,
                    OrderTypes = orderTypes,
                    OrderSide = orderSide,
                    StartDate = startDate,
                    EndDate = endDate,
                    OrderPlacementSource = orderPlacementSource,
                    ContractExpiryType = contractExpiryType,
                    AssetFilters = assetFilters,
                    RetailPortfolioId = retailPortfolioId,
                    Limit = limit,
                    Cursor = cursor,
                    SortBy = sortBy
                };

                var result = await _coinbaseOrderClient.ListOrdersAsync(ordersRequest);
                return result;
            }
            catch (CoinbaseApiException ex)
            {
                return StatusCode(502, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
} 