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
            
            // Handle order_ids array
            if (fillsRequest.OrderIds?.Length > 0)
            {
                for (int i = 0; i < fillsRequest.OrderIds.Length; i++)
                {
                    if (!string.IsNullOrEmpty(fillsRequest.OrderIds[i]))
                        query.Add("order_ids", fillsRequest.OrderIds[i]);
                }
            }
            
            // Handle trade_ids array
            if (fillsRequest.TradeIds?.Length > 0)
            {
                for (int i = 0; i < fillsRequest.TradeIds.Length; i++)
                {
                    if (!string.IsNullOrEmpty(fillsRequest.TradeIds[i]))
                        query.Add("trade_ids", fillsRequest.TradeIds[i]);
                }
            }
            
            if (!string.IsNullOrEmpty(fillsRequest.ProductId))
                query["product_id"] = fillsRequest.ProductId;
            
            // Handle product_ids array
            if (fillsRequest.ProductIds?.Length > 0)
            {
                for (int i = 0; i < fillsRequest.ProductIds.Length; i++)
                {
                    if (!string.IsNullOrEmpty(fillsRequest.ProductIds[i]))
                        query.Add("product_ids", fillsRequest.ProductIds[i]);
                }
            }
            
            if (!string.IsNullOrEmpty(fillsRequest.StartSequenceTimestamp))
                query["start_sequence_timestamp"] = fillsRequest.StartSequenceTimestamp;
            
            if (!string.IsNullOrEmpty(fillsRequest.EndSequenceTimestamp))
                query["end_sequence_timestamp"] = fillsRequest.EndSequenceTimestamp;
            
            if (fillsRequest.Limit.HasValue)
                query["limit"] = fillsRequest.Limit.Value.ToString();
            
            if (!string.IsNullOrEmpty(fillsRequest.Cursor))
                query["cursor"] = fillsRequest.Cursor;
            
            if (!string.IsNullOrEmpty(fillsRequest.SortBy))
                query["sort_by"] = fillsRequest.SortBy;
            
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