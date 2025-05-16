using System.Text.Json;
using System.Text.Json.Nodes;
using crypto_bot_api.Helpers;
using crypto_bot_api.Utilities;
using crypto_bot_api.Models.DTOs.Orders;
using Microsoft.Extensions.Configuration;
using System.Web;

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

        public async Task<JsonObject> CreateOrderAsync(CreateOrderRequestDto orderRequest)
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
            return JsonSerializer.Deserialize<JsonObject>(jsonResponse) ?? new JsonObject();
        }
        
        public async Task<JsonObject> ListOrderFillsAsync(ListOrderFillsRequestDto fillsRequest)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            
            // Add query parameters if they're not null
            if (!string.IsNullOrEmpty(fillsRequest.OrderId))
                query["order_id"] = fillsRequest.OrderId;
            
            if (!string.IsNullOrEmpty(fillsRequest.ProductId))
                query["product_id"] = fillsRequest.ProductId;
            
            if (!string.IsNullOrEmpty(fillsRequest.StartSequenceTimestamp))
                query["start_sequence_timestamp"] = fillsRequest.StartSequenceTimestamp;
            
            if (!string.IsNullOrEmpty(fillsRequest.EndSequenceTimestamp))
                query["end_sequence_timestamp"] = fillsRequest.EndSequenceTimestamp;
            
            if (fillsRequest.Limit.HasValue)
                query["limit"] = fillsRequest.Limit.Value.ToString();
            
            if (!string.IsNullOrEmpty(fillsRequest.Cursor))
                query["cursor"] = fillsRequest.Cursor;
            
            string queryString = query.Count > 0 ? $"?{query}" : string.Empty;
            string endpoint = $"/api/v3/brokerage/orders/historical/fills{queryString}";
            string uri = $"GET {endpoint}";
            string fullUrl = $"{_baseUrl.TrimEnd('/')}{endpoint}";
            
            var jwt = _jwtHelper.GenerateJwt(uri);
            
            string jsonResponse = await SendAuthenticatedGetRequestAsync(jwt, fullUrl, "Failed to retrieve order fills.");
            return JsonSerializer.Deserialize<JsonObject>(jsonResponse) ?? new JsonObject();
        }
    }
} 