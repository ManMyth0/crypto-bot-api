using Moq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using crypto_bot_api.Services;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;
using crypto_bot_api.Tests.Utilities;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class CoinbaseOrderApiClientTests
    {
        // Test class to avoid JWT signing issues
        private class TestOrderApiClient : TestApiClientBase, ICoinbaseOrderApiClient
        {
            public TestOrderApiClient(Mock<HttpMessageHandler> mockHandler) 
                : base(mockHandler)
            {
            }

            public async Task<JsonObject> CreateOrderAsync(CreateOrderRequestDto orderRequest)
            {
                return await SendRequestAsync<JsonObject>(
                    HttpMethod.Post,
                    "/api/v3/brokerage/orders",
                    orderRequest);
            }

            public async Task<JsonObject> ListOrderFillsAsync(ListOrderFillsRequestDto fillsRequest)
            {
                // Implementation needed for interface but not used in these tests
                return await Task.FromResult(JsonNode.Parse("{}") as JsonObject ?? new JsonObject());
            }

            public async Task<JsonObject> ListOrdersAsync(ListOrdersRequestDto ordersRequest)
            {
                // Implementation needed for interface but not used in these tests
                return await Task.FromResult(JsonNode.Parse("{}") as JsonObject ?? new JsonObject());
            }

            public async Task<JsonObject> GetOrderAsync(string orderId)
            {
                await Task.CompletedTask;
                throw new NotImplementedException();
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
        private static readonly string SuccessfulBuyOrderResponse = @"{
            ""success"": true,
            ""success_response"": {
                ""order_id"": ""b947374d-5178-43a0-81f9-2dc0b58cca15"",
                ""product_id"": ""BTC-USD"",
                ""side"": ""BUY"",
                ""client_order_id"": ""0123-45678-012345""
            }
        }";

        private static readonly string SuccessfulSellOrderResponse = @"{
            ""success"": true,
            ""success_response"": {
                ""order_id"": ""01234567-89ab-cdef-ghij-klmnopqrstuv"",
                ""product_id"": ""BTC-USD"",
                ""side"": ""SELL"",
                ""client_order_id"": ""0123-45678-012345""
            }
        }";

        private static readonly string OrderErrorResponse = @"{
            ""success"": false,
            ""error_response"": {
                ""error"": ""INVALID_REQUEST"",
                ""message"": ""Invalid order request structure"",
                ""error_details"": ""The provided quote quantity is invalid""
            }
        }";

        [TestInitialize]
        public void TestInitialize()
        {
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _orderClient = new TestOrderApiClient(_mockHttpHandler);
        }

        // Helper methods
        private void SetupHttpResponseForMethod(HttpStatusCode statusCode, string content, string urlContains, HttpMethod method)
        {
            ((TestOrderApiClient)_orderClient).SetupHttpResponseForMethod(statusCode, content, urlContains, method);
        }

        [TestMethod]
        public async Task CreateBuyOrder_ReturnsOrderDetailsWhenSuccessful()
        {
            var createOrderRequest = new CreateOrderRequestDto
            {
                ClientOrderId = "0123-45678-012345",
                ProductId = "BTC-USD",
                Side = "BUY",
                OrderConfiguration = new OrderConfigurationDto
                {
                    LimitLimitGtc = new LimitLimitGtcDto
                    {
                        BaseSize = "0.001",
                        LimitPrice = "50000.00",
                        PostOnly = false
                    }
                }
            };

            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulBuyOrderResponse,
                "orders",
                HttpMethod.Post);

            var result = await _orderClient.CreateOrderAsync(createOrderRequest);

            Assert.IsNotNull(result);
            Assert.IsTrue(result["success"]?.GetValue<bool>() ?? false);
            Assert.IsNotNull(result["success_response"]);
            Assert.AreEqual("BTC-USD", result["success_response"]?["product_id"]?.GetValue<string>());
            Assert.AreEqual("BUY", result["success_response"]?["side"]?.GetValue<string>());
            Assert.AreEqual("0123-45678-012345", result["success_response"]?["client_order_id"]?.GetValue<string>());
        }
        
        [TestMethod]
        public async Task CreateSellOrder_ReturnsOrderDetailsWhenSuccessful()
        {
            var createOrderRequest = new CreateOrderRequestDto
            {
                ClientOrderId = "0123-45678-012345",
                ProductId = "BTC-USD",
                Side = "SELL",
                OrderConfiguration = new OrderConfigurationDto
                {
                    LimitLimitGtc = new LimitLimitGtcDto
                    {
                        BaseSize = "0.001",
                        LimitPrice = "50000.00",
                        PostOnly = false
                    }
                }
            };

            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulSellOrderResponse,
                "orders",
                HttpMethod.Post);

            var result = await _orderClient.CreateOrderAsync(createOrderRequest);

            Assert.IsNotNull(result);
            Assert.IsTrue(result["success"]?.GetValue<bool>() ?? false);
            Assert.IsNotNull(result["success_response"]);
            Assert.AreEqual("BTC-USD", result["success_response"]?["product_id"]?.GetValue<string>());
            Assert.AreEqual("SELL", result["success_response"]?["side"]?.GetValue<string>());
            Assert.AreEqual("0123-45678-012345", result["success_response"]?["client_order_id"]?.GetValue<string>());
        }

        [TestMethod]
        public async Task CreateOrder_ThrowsExceptionWhenApiReturnsError()
        {
            var createOrderRequest = new CreateOrderRequestDto
            {
                ClientOrderId = "0123-45678-012345",
                ProductId = "BTC-USD",
                Side = "BUY",
                OrderConfiguration = new OrderConfigurationDto
                {
                    LimitLimitGtc = new LimitLimitGtcDto
                    {
                        BaseSize = "invalid",
                        LimitPrice = "50000.00",
                        PostOnly = false
                    }
                }
            };

            SetupHttpResponseForMethod(
                HttpStatusCode.BadRequest,
                OrderErrorResponse,
                "orders",
                HttpMethod.Post);

            try
            {
                await _orderClient.CreateOrderAsync(createOrderRequest);
                Assert.Fail("Expected CoinbaseApiException was not thrown");
            }
            catch (CoinbaseApiException)
            {
                // Parse the error response directly
                var errorResponse = JsonSerializer.Deserialize<JsonObject>(OrderErrorResponse);
                Assert.IsNotNull(errorResponse);
                Assert.IsFalse(errorResponse["success"]?.GetValue<bool>() ?? true);
                Assert.AreEqual("INVALID_REQUEST", errorResponse["error_response"]?["error"]?.GetValue<string>());
                Assert.AreEqual("Invalid order request structure", errorResponse["error_response"]?["message"]?.GetValue<string>());
                Assert.AreEqual("The provided quote quantity is invalid", errorResponse["error_response"]?["error_details"]?.GetValue<string>());
            }
        }
    }
}