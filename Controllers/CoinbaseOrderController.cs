using crypto_bot_api.Services;
using Microsoft.AspNetCore.Mvc;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;

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
        public async Task<ActionResult<CreateOrderResponseDto>> CreateOrder([FromBody] CreateOrderRequestDto orderRequest)
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
    }
} 