using crypto_bot_api.Services;
using Microsoft.AspNetCore.Mvc;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;
using System.Text.Json.Nodes;

namespace crypto_bot_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoinbaseOrderController : ControllerBase
    {
        private readonly ICoinbaseOrderApiClient _coinbaseOrderClient;
        
        public CoinbaseOrderController(ICoinbaseOrderApiClient coinbaseOrderClient)
        {
            _coinbaseOrderClient = coinbaseOrderClient;
        }

        [HttpPost("orders")]
        public async Task<ActionResult<JsonObject>> CreateOrder([FromBody] CreateOrderRequestDto orderRequest)
        {
            try
            {
                var result = await _coinbaseOrderClient.CreateOrderAsync(orderRequest);
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

        [HttpGet("historical/fills")]
        public async Task<ActionResult<JsonObject>> GetOrderFills(
            [FromQuery] string? orderId = null,
            [FromQuery] string? productId = null,
            [FromQuery] string? startSequenceTimestamp = null,
            [FromQuery] string? endSequenceTimestamp = null,
            [FromQuery] int? limit = null,
            [FromQuery] string? cursor = null)
        {
            try
            {
                var fillsRequest = new ListOrderFillsRequestDto
                {
                    OrderId = orderId,
                    ProductId = productId,
                    StartSequenceTimestamp = startSequenceTimestamp,
                    EndSequenceTimestamp = endSequenceTimestamp,
                    Limit = limit,
                    Cursor = cursor
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
    }
} 