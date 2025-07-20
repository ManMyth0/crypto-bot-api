using System.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using crypto_bot_api.Helpers;
using crypto_bot_api.Utilities;
using Microsoft.Extensions.Options;
using crypto_bot_api.Models.DTOs.Orders;

namespace crypto_bot_api.Services
{
    public class CoinbaseOrderApiClient : BaseCoinbaseApiClient, ICoinbaseOrderApiClient
    {
        private readonly new Ed25519JwtHelper _jwtHelper;
        private readonly HttpClient _httpClient;
        private new readonly IConfiguration _configuration;

        public CoinbaseOrderApiClient(
            HttpClient client, 
            IConfiguration config,
            IOptions<SandboxConfiguration> sandboxConfig)
            : base(client, config, sandboxConfig)
        {
            // Create Ed25519 JWT helper
            _jwtHelper = new Ed25519JwtHelper(_apiKeyId, _apiSecret);
            _httpClient = client;
            _configuration = config;
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

        public async Task<JsonObject> ListOrdersAsync(ListOrdersRequestDto ordersRequest)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            
            // Handle order_ids array
            if (ordersRequest.OrderIds?.Length > 0)
            {
                foreach (var orderId in ordersRequest.OrderIds)
                {
                    if (!string.IsNullOrEmpty(orderId))
                        query.Add("order_ids", orderId);
                }
            }
            
            // Handle product_ids array
            if (ordersRequest.ProductIds?.Length > 0)
            {
                foreach (var productId in ordersRequest.ProductIds)
                {
                    if (!string.IsNullOrEmpty(productId))
                        query.Add("product_ids", productId);
                }
            }
            
            if (!string.IsNullOrEmpty(ordersRequest.ProductType))
                query["product_type"] = ordersRequest.ProductType;
            
            // Handle order_status array
            if (ordersRequest.OrderStatus?.Length > 0)
            {
                foreach (var status in ordersRequest.OrderStatus)
                {
                    if (!string.IsNullOrEmpty(status))
                        query.Add("order_status", status);
                }
            }
            
            // Handle time_in_forces array
            if (ordersRequest.TimeInForces?.Length > 0)
            {
                foreach (var timeInForce in ordersRequest.TimeInForces)
                {
                    if (!string.IsNullOrEmpty(timeInForce))
                        query.Add("time_in_forces", timeInForce);
                }
            }
            
            // Handle order_types array
            if (ordersRequest.OrderTypes?.Length > 0)
            {
                foreach (var orderType in ordersRequest.OrderTypes)
                {
                    if (!string.IsNullOrEmpty(orderType))
                        query.Add("order_types", orderType);
                }
            }
            
            if (!string.IsNullOrEmpty(ordersRequest.OrderSide))
                query["order_side"] = ordersRequest.OrderSide;
            
            if (!string.IsNullOrEmpty(ordersRequest.StartDate))
                query["start_date"] = ordersRequest.StartDate;
            
            if (!string.IsNullOrEmpty(ordersRequest.EndDate))
                query["end_date"] = ordersRequest.EndDate;
            
            if (!string.IsNullOrEmpty(ordersRequest.OrderPlacementSource))
                query["order_placement_source"] = ordersRequest.OrderPlacementSource;
            
            if (!string.IsNullOrEmpty(ordersRequest.ContractExpiryType))
                query["contract_expiry_type"] = ordersRequest.ContractExpiryType;
            
            // Handle asset_filters array
            if (ordersRequest.AssetFilters?.Length > 0)
            {
                foreach (var assetFilter in ordersRequest.AssetFilters)
                {
                    if (!string.IsNullOrEmpty(assetFilter))
                        query.Add("asset_filters", assetFilter);
                }
            }
            
            if (!string.IsNullOrEmpty(ordersRequest.RetailPortfolioId))
                query["retail_portfolio_id"] = ordersRequest.RetailPortfolioId;
            
            if (ordersRequest.Limit.HasValue)
                query["limit"] = ordersRequest.Limit.Value.ToString();
            
            if (!string.IsNullOrEmpty(ordersRequest.Cursor))
                query["cursor"] = ordersRequest.Cursor;
            
            if (!string.IsNullOrEmpty(ordersRequest.SortBy))
                query["sort_by"] = ordersRequest.SortBy;
            
            string queryString = query.Count > 0 ? $"?{query}" : string.Empty;
            string endpoint = $"/api/v3/brokerage/orders/historical/batch{queryString}";
            string uri = $"GET {endpoint}";
            string fullUrl = $"{_baseUrl.TrimEnd('/')}{endpoint}";
            
            var jwt = _jwtHelper.GenerateJwt(uri);
            
            string jsonResponse = await SendAuthenticatedGetRequestAsync(jwt, fullUrl, "Failed to retrieve orders.");
            return JsonSerializer.Deserialize<JsonObject>(jsonResponse) ?? new JsonObject();
        }

        public async Task<JsonObject> GetOrderAsync(string orderId)
        {
            var response = await _httpClient.GetAsync($"/api/v3/brokerage/orders/{orderId}");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonNode.Parse(content)?.AsObject() ?? new JsonObject();
        }
    }
} 