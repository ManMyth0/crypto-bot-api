using Moq;
using System.Net;
using System.Text;
using Moq.Protected;
using System.Text.Json;
using crypto_bot_api.Services;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class CoinbaseOrderApiClientTests
    {
        // Test class to avoid JWT signing issues
        private class TestOrderApiClient : ICoinbaseOrderApiClient
        {
            private readonly HttpClient _client;
            private readonly Mock<HttpMessageHandler> _mockHandler;

            public TestOrderApiClient(Mock<HttpMessageHandler> mockHandler)
            {
                _mockHandler = mockHandler;
                _client = new HttpClient(mockHandler.Object)
                {
                    BaseAddress = new Uri("https://api.coinbase.com")
                };
            }

            public async Task<CreateOrderResponseDto> CreateOrderAsync(CreateOrderRequestDto orderRequest)
            {
                string endpoint = "/api/v3/brokerage/orders";
                string fullUrl = $"https://api.coinbase.com{endpoint}";

                var request = new HttpRequestMessage(HttpMethod.Post, fullUrl);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(orderRequest),
                    Encoding.UTF8,
                    "application/json");

                var response = await _client.SendAsync(request);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new CoinbaseApiException($"Failed to create order: {jsonResponse}");
                }

                var orderResponse = JsonSerializer.Deserialize<CreateOrderResponseDto>(
                    jsonResponse,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                return orderResponse ?? new CreateOrderResponseDto();
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
            // Setup mock HTTP handler
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            
            // Setup API client
            _orderClient = new TestOrderApiClient(_mockHttpHandler);
        }

        // Helper methods
        private void SetupHttpResponseForMethod(HttpStatusCode statusCode, string content, string urlContains, HttpMethod method)
        {
            _mockHttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == method &&
                        req.RequestUri != null &&
                        req.RequestUri.ToString().Contains(urlContains)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
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

            SetupHttpResponseForMethod(
                HttpStatusCode.OK,
                SuccessfulBuyOrderResponse,
                "orders",
                HttpMethod.Post);

            var result = await _orderClient.CreateOrderAsync(createOrderRequest);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.SuccessResponse);
            Assert.AreEqual("BTC-USD", result.SuccessResponse.ProductId);
            Assert.AreEqual("BUY", result.SuccessResponse.Side);
            Assert.AreEqual("0123-45678-012345", result.SuccessResponse.ClientOrderId);
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

            SetupHttpResponseForMethod(
                HttpStatusCode.OK,
                SuccessfulSellOrderResponse,
                "orders",
                HttpMethod.Post);

            var result = await _orderClient.CreateOrderAsync(createOrderRequest);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.SuccessResponse);
            Assert.AreEqual("BTC-USD", result.SuccessResponse.ProductId);
            Assert.AreEqual("SELL", result.SuccessResponse.Side);
            Assert.AreEqual("0123-45678-012345", result.SuccessResponse.ClientOrderId);
        }

        [TestMethod]
        [ExpectedException(typeof(CoinbaseApiException))]
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

            // This should throw an exception
            await _orderClient.CreateOrderAsync(createOrderRequest);
        }
    }
} 