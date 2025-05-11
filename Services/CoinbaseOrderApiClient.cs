using System.Text.Json;
using crypto_bot_api.Helpers;
using crypto_bot_api.Utilities;
using crypto_bot_api.Models.DTOs.Orders;
using Microsoft.Extensions.Configuration;

namespace crypto_bot_api.Services
{
    public class CoinbaseOrderApiClient : BaseCoinbaseApiClient, ICoinbaseOrderApiClient
    {
        private readonly new Ed25519JwtHelper _jwtHelper;

        public CoinbaseOrderApiClient(HttpClient client, IConfiguration config)
            : base(client, config)
        {
            // Create Ed25519 JWT helper
            _jwtHelper = new Ed25519JwtHelper(_apiKeyId, _apiSecret);
        }

        public async Task<CreateOrderResponseDto> CreateOrderAsync(CreateOrderRequestDto orderRequest)
        {
            // Generate a client order ID if one is not provided
            if (string.IsNullOrEmpty(orderRequest.ClientOrderId))
            {
                orderRequest.ClientOrderId = ClientOrderIdGenerator.GenerateCoinbaseClientOrderId();
            }
            
            string endpoint = "/api/v3/brokerage/orders";
            string uri = $"POST {endpoint}";
            string fullUrl = $"{_baseUrl.TrimEnd('/')}{endpoint}";
            
            var jwt = _jwtHelper.GenerateJwt(uri);
            
            // Serialize the order request to JSON
            string requestContent = JsonSerializer.Serialize(orderRequest);
            
            string jsonResponse = await SendAuthenticatedPostRequestAsync(jwt, fullUrl, requestContent, "Failed to create order.");
            return JsonSerializer.Deserialize<CreateOrderResponseDto>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                ?? new CreateOrderResponseDto();
        }
    }
} 