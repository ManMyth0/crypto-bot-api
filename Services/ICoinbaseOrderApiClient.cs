using System.Text.Json.Nodes;
using crypto_bot_api.Models.DTOs.Orders;

namespace crypto_bot_api.Services
{
    public interface ICoinbaseOrderApiClient
    {
        Task<JsonObject> CreateOrderAsync(CreateOrderRequestDto orderRequest);
        Task<JsonObject> ListOrderFillsAsync(ListOrderFillsRequestDto fillsRequest);
        Task<JsonObject> ListOrdersAsync(ListOrdersRequestDto ordersRequest);
        Task<JsonObject> GetOrderAsync(string orderId);
    }
} 