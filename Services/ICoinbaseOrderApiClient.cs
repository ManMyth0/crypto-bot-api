using crypto_bot_api.Models.DTOs.Orders;

namespace crypto_bot_api.Services
{
    public interface ICoinbaseOrderApiClient
    {
        Task<CreateOrderResponseDto> CreateOrderAsync(CreateOrderRequestDto orderRequest);
    }
} 