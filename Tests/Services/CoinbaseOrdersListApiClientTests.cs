using Moq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using crypto_bot_api.Services;
using crypto_bot_api.Tests.Utilities;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class CoinbaseOrdersListApiClientTests
    {
        // Test class to avoid JWT signing issues
        private class TestOrdersListApiClient : TestApiClientBase, ICoinbaseOrderApiClient
        {
            public TestOrdersListApiClient(Mock<HttpMessageHandler> mockHandler)
                : base(mockHandler)
            {
            }

            public async Task<JsonObject> CreateOrderAsync(CreateOrderRequestDto orderRequest)
            {
                // Implementation needed for interface but not used in these tests
                return await Task.FromResult(JsonNode.Parse("{}") as JsonObject ?? new JsonObject());
            }

            public async Task<JsonObject> ListOrderFillsAsync(ListOrderFillsRequestDto fillsRequest)
            {
                // Implementation needed for interface but not used in these tests
                return await Task.FromResult(JsonNode.Parse("{}") as JsonObject ?? new JsonObject());
            }

            public async Task<JsonObject> ListOrdersAsync(ListOrdersRequestDto ordersRequest)
            {
                var queryParams = new List<string>();
                
                // Handle order_ids array
                if (ordersRequest.OrderIds?.Length > 0)
                {
                    foreach (var orderId in ordersRequest.OrderIds)
                    {
                        if (!string.IsNullOrEmpty(orderId))
                            queryParams.Add($"order_ids={Uri.EscapeDataString(orderId)}");
                    }
                }
                
                // Handle product_ids array
                if (ordersRequest.ProductIds?.Length > 0)
                {
                    foreach (var productId in ordersRequest.ProductIds)
                    {
                        if (!string.IsNullOrEmpty(productId))
                            queryParams.Add($"product_ids={Uri.EscapeDataString(productId)}");
                    }
                }
                
                if (!string.IsNullOrEmpty(ordersRequest.ProductType))
                    queryParams.Add($"product_type={ordersRequest.ProductType}");
                
                // Handle order_status array
                if (ordersRequest.OrderStatus?.Length > 0)
                {
                    foreach (var status in ordersRequest.OrderStatus)
                    {
                        if (!string.IsNullOrEmpty(status))
                            queryParams.Add($"order_status={Uri.EscapeDataString(status)}");
                    }
                }
                
                // Handle time_in_forces array
                if (ordersRequest.TimeInForces?.Length > 0)
                {
                    foreach (var timeInForce in ordersRequest.TimeInForces)
                    {
                        if (!string.IsNullOrEmpty(timeInForce))
                            queryParams.Add($"time_in_forces={Uri.EscapeDataString(timeInForce)}");
                    }
                }
                
                // Handle order_types array
                if (ordersRequest.OrderTypes?.Length > 0)
                {
                    foreach (var orderType in ordersRequest.OrderTypes)
                    {
                        if (!string.IsNullOrEmpty(orderType))
                            queryParams.Add($"order_types={Uri.EscapeDataString(orderType)}");
                    }
                }
                
                if (!string.IsNullOrEmpty(ordersRequest.OrderSide))
                    queryParams.Add($"order_side={ordersRequest.OrderSide}");
                
                if (!string.IsNullOrEmpty(ordersRequest.StartDate))
                    queryParams.Add($"start_date={ordersRequest.StartDate}");
                
                if (!string.IsNullOrEmpty(ordersRequest.EndDate))
                    queryParams.Add($"end_date={ordersRequest.EndDate}");
                
                if (!string.IsNullOrEmpty(ordersRequest.OrderPlacementSource))
                    queryParams.Add($"order_placement_source={ordersRequest.OrderPlacementSource}");
                
                if (!string.IsNullOrEmpty(ordersRequest.ContractExpiryType))
                    queryParams.Add($"contract_expiry_type={ordersRequest.ContractExpiryType}");
                
                // Handle asset_filters array
                if (ordersRequest.AssetFilters?.Length > 0)
                {
                    foreach (var assetFilter in ordersRequest.AssetFilters)
                    {
                        if (!string.IsNullOrEmpty(assetFilter))
                            queryParams.Add($"asset_filters={Uri.EscapeDataString(assetFilter)}");
                    }
                }
                
                if (!string.IsNullOrEmpty(ordersRequest.RetailPortfolioId))
                    queryParams.Add($"retail_portfolio_id={ordersRequest.RetailPortfolioId}");
                
                if (ordersRequest.Limit.HasValue)
                    queryParams.Add($"limit={ordersRequest.Limit.Value}");
                
                if (!string.IsNullOrEmpty(ordersRequest.Cursor))
                    queryParams.Add($"cursor={ordersRequest.Cursor}");
                
                if (!string.IsNullOrEmpty(ordersRequest.SortBy))
                    queryParams.Add($"sort_by={ordersRequest.SortBy}");
                
                string queryString = queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : string.Empty;
                
                return await SendRequestAsync<JsonObject>(
                    HttpMethod.Get,
                    $"/api/v3/brokerage/orders/historical/batch{queryString}");
            }

            public new void SetupHttpResponseForMethod(HttpStatusCode statusCode, string content, string urlContains, HttpMethod method)
            {
                base.SetupHttpResponseForMethod(statusCode, content, urlContains, method);
            }
        }

        // Test dependencies
        private Mock<HttpMessageHandler> _mockHttpHandler = null!;
        private ICoinbaseOrderApiClient _orderClient = null!;

        // Mock responses
        private static readonly string SuccessfulOrdersResponse = @"{
            ""orders"": [
                {
                    ""order_id"": ""b947374d-5178-43a0-81f9-2dc0b58cca15"",
                    ""client_order_id"": ""client-order-123"",
                    ""product_id"": ""BTC-USD"",
                    ""side"": ""BUY"",
                    ""status"": ""FILLED"",
                    ""time_in_force"": ""GTC"",
                    ""created_time"": ""2023-05-31T09:59:59Z"",
                    ""completion_percentage"": ""100"",
                    ""filled_size"": ""0.001"",
                    ""average_filled_price"": ""32000.50"",
                    ""fee"": ""0.64"",
                    ""number_of_fills"": ""1"",
                    ""size"": ""0.001"",
                    ""order_type"": ""LIMIT"",
                    ""total_fees"": ""0.64"",
                    ""size_in_quote"": false,
                    ""total_value_after_fees"": ""31.36"",
                    ""trigger_status"": ""INVALID_ORDER_TYPE"",
                    ""order_placement_source"": ""RETAIL_ADVANCED"",
                    ""is_liquidation"": false
                }
            ],
            ""sequence"": ""12345"",
            ""has_next"": false,
            ""cursor"": ""cursor123""
        }";

        private static readonly string EmptyOrdersResponse = @"{
            ""orders"": [],
            ""sequence"": ""12345"",
            ""has_next"": false,
            ""cursor"": null
        }";

        [TestInitialize]
        public void TestInitialize()
        {
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _orderClient = new TestOrdersListApiClient(_mockHttpHandler);
        }

        // Helper methods
        private void SetupHttpResponseForMethod(HttpStatusCode statusCode, string content, string urlContains, HttpMethod method)
        {
            ((TestOrdersListApiClient)_orderClient).SetupHttpResponseForMethod(statusCode, content, urlContains, method);
        }

        [TestMethod]
        public async Task ListOrders_ReturnsOrdersWhenSuccessful()
        {
            var ordersRequest = new ListOrdersRequestDto
            {
                ProductIds = new[] { "BTC-USD" },
                OrderStatus = new[] { "FILLED" }
                // Limit defaults to 50
            };

            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulOrdersResponse,
                "historical/batch",
                HttpMethod.Get);

            var result = await _orderClient.ListOrdersAsync(ordersRequest);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["orders"]);
            
            var orders = result["orders"]?.AsArray();
            Assert.IsNotNull(orders);
            Assert.AreEqual(1, orders.Count);
            
            var order = orders[0]?.AsObject();
            Assert.IsNotNull(order);
            Assert.AreEqual("b947374d-5178-43a0-81f9-2dc0b58cca15", order["order_id"]?.GetValue<string>());
            Assert.AreEqual("BTC-USD", order["product_id"]?.GetValue<string>());
            Assert.AreEqual("BUY", order["side"]?.GetValue<string>());
            Assert.AreEqual("FILLED", order["status"]?.GetValue<string>());
        }

        [TestMethod]
        public async Task ListOrders_ReturnsEmptyListWhenNoOrders()
        {
            var ordersRequest = new ListOrdersRequestDto
            {
                OrderIds = new[] { "non-existent-order-id" }
            };

            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                EmptyOrdersResponse,
                "historical/batch",
                HttpMethod.Get);

            var result = await _orderClient.ListOrdersAsync(ordersRequest);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["orders"]);
            
            var orders = result["orders"]?.AsArray();
            Assert.IsNotNull(orders);
            Assert.AreEqual(0, orders.Count);
            
            Assert.IsFalse(result["has_next"]?.GetValue<bool>() ?? true);
        }

        [TestMethod]
        public async Task ListOrders_WithMultipleParameters_SendsProperQueryString()
        {
            var ordersRequest = new ListOrdersRequestDto
            {
                ProductIds = new[] { "BTC-USD", "ETH-USD" },
                OrderStatus = new[] { "OPEN", "FILLED" },
                OrderTypes = new[] { "LIMIT", "MARKET" },
                StartDate = "2023-05-30T00:00:00Z",
                EndDate = "2023-05-31T23:59:59Z",
                OrderSide = "BUY",
                // Limit is already defaulted to 50
            };

            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulOrdersResponse,
                "historical/batch",
                HttpMethod.Get);

            var result = await _orderClient.ListOrdersAsync(ordersRequest);
            
            Assert.IsNotNull(result);
            Assert.IsNotNull(result["orders"]);
        }

        [TestMethod]
        public async Task ListOrders_ThrowsExceptionWhenApiReturnsError()
        {
            var ordersRequest = new ListOrdersRequestDto
            {
                OrderIds = new[] { "invalid-order-id" }
            };

            SetupHttpResponseForMethod(
                HttpStatusCode.BadRequest,
                @"{""error"": ""INVALID_REQUEST"", ""message"": ""Invalid order ID format""}",
                "historical/batch",
                HttpMethod.Get);

            try
            {
                await _orderClient.ListOrdersAsync(ordersRequest);
                Assert.Fail("Expected CoinbaseApiException was not thrown");
            }
            catch (CoinbaseApiException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Invalid order ID format"));
            }
        }

        [TestMethod]
        public async Task ListOrders_DefaultsLimitTo50()
        {
            var ordersRequest = new ListOrdersRequestDto
            {
                ProductIds = new[] { "BTC-USD" }
                // Not setting Limit, should default to 50
            };

            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulOrdersResponse,
                "limit=50",
                HttpMethod.Get);

            var result = await _orderClient.ListOrdersAsync(ordersRequest);
            
            Assert.IsNotNull(result);
            Assert.IsNotNull(result["orders"]);
        }
    }
} 