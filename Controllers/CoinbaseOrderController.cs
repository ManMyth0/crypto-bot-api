using crypto_bot_api.Services;
using Microsoft.AspNetCore.Mvc;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;
using System.Text.Json.Nodes;
using crypto_bot_api.Models;

namespace crypto_bot_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoinbaseOrderController : ControllerBase
    {
        private readonly ICoinbaseOrderApiClient _coinbaseOrderClient;
        private readonly IAssembleOrderDetailsService _assembleOrderDetails;
        private readonly IPositionManagementService _positionManager;
        
        public CoinbaseOrderController(
            ICoinbaseOrderApiClient coinbaseOrderClient,
            IAssembleOrderDetailsService assembleOrderDetails,
            IPositionManagementService positionManager)
        {
            _coinbaseOrderClient = coinbaseOrderClient;
            _assembleOrderDetails = assembleOrderDetails;
            _positionManager = positionManager;
        }

        [HttpPost("orders")]
        public async Task<JsonNode?> CreateOrderAsync([FromBody] CreateOrderRequestDto orderRequest)
        {
            try
            {
                // Create order on Coinbase
                var result = await _coinbaseOrderClient.CreateOrderAsync(orderRequest);
                
                // Get order details including fills
                var orderId = result["order_id"]?.GetValue<string>();
                if (string.IsNullOrEmpty(orderId))
                    return result;

                var fillsResult = await _coinbaseOrderClient.ListOrderFillsAsync(new ListOrderFillsRequestDto 
                { 
                    OrderId = orderId 
                });

                var fills = fillsResult["fills"]?.AsArray();
                if (fills == null || fills.Count == 0)
                    return result;

                // Get the size from the order configuration
                decimal? orderSize = null;
                if (orderRequest.OrderConfiguration?.LimitLimitGtc != null)
                {
                    _ = decimal.TryParse(orderRequest.OrderConfiguration.LimitLimitGtc.BaseSize, out decimal size);
                    orderSize = size;
                }
                else if (orderRequest.OrderConfiguration?.LimitLimitGtd != null)
                {
                    _ = decimal.TryParse(orderRequest.OrderConfiguration.LimitLimitGtd.BaseSize, out decimal size);
                    orderSize = size;
                }

                // Assemble order details
                var orderDetails = _assembleOrderDetails.AssembleFromFills(
                    orderId,
                    fills,
                    result["status"]?.GetValue<string>(),
                    orderSize);

                if (orderDetails == null)
                    return result;

                // Create or update position based on order type
                if (orderRequest.PositionId.HasValue)
                {
                    await _positionManager.UpdatePositionFromClosingOrderAsync(orderDetails, orderRequest.PositionId.Value);
                }
                else
                {
                    await _positionManager.CreatePositionFromOrderAsync(orderDetails);
                }

                return result;
            }
            catch (CoinbaseApiException)
            {
                throw;
            }
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