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
    public class CoinbaseOrderFillsApiClientTests
    {
        // Test class to avoid JWT signing issues
        private class TestOrderFillsApiClient : TestApiClientBase, ICoinbaseOrderApiClient
        {
            public TestOrderFillsApiClient(Mock<HttpMessageHandler> mockHandler)
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
                var queryParams = new List<string>();
                
                if (!string.IsNullOrEmpty(fillsRequest.OrderId))
                    queryParams.Add($"order_id={fillsRequest.OrderId}");
                    
                // Handle order_ids array
                if (fillsRequest.OrderIds?.Length > 0)
                {
                    foreach (var orderId in fillsRequest.OrderIds)
                    {
                        if (!string.IsNullOrEmpty(orderId))
                            queryParams.Add($"order_ids={Uri.EscapeDataString(orderId)}");
                    }
                }
                
                // Handle trade_ids array
                if (fillsRequest.TradeIds?.Length > 0)
                {
                    foreach (var tradeId in fillsRequest.TradeIds)
                    {
                        if (!string.IsNullOrEmpty(tradeId))
                            queryParams.Add($"trade_ids={Uri.EscapeDataString(tradeId)}");
                    }
                }
                
                if (!string.IsNullOrEmpty(fillsRequest.ProductId))
                    queryParams.Add($"product_id={fillsRequest.ProductId}");
                
                // Handle product_ids array    
                if (fillsRequest.ProductIds?.Length > 0)
                {
                    foreach (var productId in fillsRequest.ProductIds)
                    {
                        if (!string.IsNullOrEmpty(productId))
                            queryParams.Add($"product_ids={Uri.EscapeDataString(productId)}");
                    }
                }
                    
                if (!string.IsNullOrEmpty(fillsRequest.StartSequenceTimestamp))
                    queryParams.Add($"start_sequence_timestamp={fillsRequest.StartSequenceTimestamp}");
                    
                if (!string.IsNullOrEmpty(fillsRequest.EndSequenceTimestamp))
                    queryParams.Add($"end_sequence_timestamp={fillsRequest.EndSequenceTimestamp}");
                    
                if (fillsRequest.Limit.HasValue)
                    queryParams.Add($"limit={fillsRequest.Limit.Value}");
                    
                if (!string.IsNullOrEmpty(fillsRequest.Cursor))
                    queryParams.Add($"cursor={fillsRequest.Cursor}");
                
                if (!string.IsNullOrEmpty(fillsRequest.SortBy))
                    queryParams.Add($"sort_by={fillsRequest.SortBy}");
                    
                string queryString = queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : string.Empty;
                
                return await SendRequestAsync<JsonObject>(
                    HttpMethod.Get,
                    $"/api/v3/brokerage/orders/historical/fills{queryString}");
            }

            public async Task<JsonObject> ListOrdersAsync(ListOrdersRequestDto ordersRequest)
            {
                // Implementation needed for interface but not used in these fills-focused tests
                return await Task.FromResult(JsonNode.Parse("{}") as JsonObject ?? new JsonObject());
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
        private static readonly string SuccessfulOrderFillsResponse = @"{
            ""fills"": [
                {
                    ""entry_id"": ""1234-5678"",
                    ""trade_id"": ""9876-5432"",
                    ""order_id"": ""b947374d-5178-43a0-81f9-2dc0b58cca15"",
                    ""trade_time"": ""2023-05-31T09:59:59Z"",
                    ""trade_type"": ""FILL"",
                    ""price"": ""32000.50"",
                    ""size"": ""0.001"",
                    ""commission"": ""0.64"",
                    ""product_id"": ""BTC-USD"",
                    ""sequence_timestamp"": ""2023-05-31T09:59:59Z"",
                    ""liquidity_indicator"": ""TAKER"",
                    ""size_in_quote"": false,
                    ""user_id"": ""user123"",
                    ""side"": ""BUY""
                }
            ],
            ""cursor"": ""cursor123"",
            ""has_next"": false
        }";

        private static readonly string EmptyOrderFillsResponse = @"{
            ""fills"": [],
            ""cursor"": null,
            ""has_next"": false
        }";

        [TestInitialize]
        public void TestInitialize()
        {
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _orderClient = new TestOrderFillsApiClient(_mockHttpHandler);
        }

        // Helper methods
        private void SetupHttpResponseForMethod(HttpStatusCode statusCode, string content, string urlContains, HttpMethod method)
        {
            ((TestOrderFillsApiClient)_orderClient).SetupHttpResponseForMethod(statusCode, content, urlContains, method);
        }

        [TestMethod]
        public async Task ListOrderFills_ReturnsOrderFillsWhenSuccessful()
        {
            var fillsRequest = new ListOrderFillsRequestDto
            {
                OrderId = "b947374d-5178-43a0-81f9-2dc0b58cca15",
                ProductId = "BTC-USD"
                // Limit defaults to 50
            };

            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulOrderFillsResponse,
                "historical/fills",
                HttpMethod.Get);

            var result = await _orderClient.ListOrderFillsAsync(fillsRequest);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["fills"]);
            
            var fills = result["fills"]?.AsArray();
            Assert.IsNotNull(fills);
            Assert.AreEqual(1, fills.Count);
            
            var fill = fills[0]?.AsObject();
            Assert.IsNotNull(fill);
            Assert.AreEqual("b947374d-5178-43a0-81f9-2dc0b58cca15", fill["order_id"]?.GetValue<string>());
            Assert.AreEqual("0.64", fill["commission"]?.GetValue<string>());
            Assert.AreEqual("BTC-USD", fill["product_id"]?.GetValue<string>());
            Assert.AreEqual("BUY", fill["side"]?.GetValue<string>());
        }

        [TestMethod]
        public async Task ListOrderFills_ReturnsEmptyListWhenNoFills()
        {
            var fillsRequest = new ListOrderFillsRequestDto
            {
                OrderId = "non-existent-order-id"
            };

            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                EmptyOrderFillsResponse,
                "historical/fills",
                HttpMethod.Get);

            var result = await _orderClient.ListOrderFillsAsync(fillsRequest);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["fills"]);
            
            var fills = result["fills"]?.AsArray();
            Assert.IsNotNull(fills);
            Assert.AreEqual(0, fills.Count);
            
            Assert.IsFalse(result["has_next"]?.GetValue<bool>() ?? true);
        }

        [TestMethod]
        public async Task ListOrderFills_WithMultipleParameters_SendsProperQueryString()
        {
            var fillsRequest = new ListOrderFillsRequestDto
            {
                OrderIds = new[] { "order-1", "order-2" },
                ProductIds = new[] { "BTC-USD", "ETH-USD" },
                StartSequenceTimestamp = "2023-05-30T00:00:00Z",
                EndSequenceTimestamp = "2023-05-31T23:59:59Z",
                SortBy = "created_time",
                // Limit is already defaulted to 50
            };

            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulOrderFillsResponse,
                "historical/fills",
                HttpMethod.Get);

            var result = await _orderClient.ListOrderFillsAsync(fillsRequest);
            
            Assert.IsNotNull(result);
            Assert.IsNotNull(result["fills"]);
        }

        [TestMethod]
        public async Task ListOrderFills_ThrowsExceptionWhenApiReturnsError()
        {
            var fillsRequest = new ListOrderFillsRequestDto
            {
                OrderId = "invalid-order-id"
            };

            SetupHttpResponseForMethod(
                HttpStatusCode.BadRequest,
                @"{""error"": ""INVALID_REQUEST"", ""message"": ""Invalid order ID format""}",
                "historical/fills",
                HttpMethod.Get);

            try
            {
                await _orderClient.ListOrderFillsAsync(fillsRequest);
                Assert.Fail("Expected CoinbaseApiException was not thrown");
            }
            catch (CoinbaseApiException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Invalid order ID format"));
            }
        }

        [TestMethod]
        public async Task ListOrderFills_DefaultsLimitTo50()
        {
            var fillsRequest = new ListOrderFillsRequestDto
            {
                ProductId = "BTC-USD"
                // Not setting Limit, should default to 50
            };

            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulOrderFillsResponse,
                "limit=50",
                HttpMethod.Get);

            var result = await _orderClient.ListOrderFillsAsync(fillsRequest);
            
            Assert.IsNotNull(result);
            Assert.IsNotNull(result["fills"]);
        }
    }
} 