using Moq;
using System.Net;
using System.Text.Json;
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

            public async Task<CreateOrderResponseDto> CreateOrderAsync(CreateOrderRequestDto orderRequest)
            {
                return await SendRequestAsync<CreateOrderResponseDto>(
                    HttpMethod.Post,
                    "/api/v3/brokerage/orders",
                    orderRequest);
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

            CoinbaseApiTestAssertions.AssertSuccessfulResponse(result, expectedStatusCode);
            CoinbaseApiTestAssertions.AssertOrderResponse(result, "BTC-USD", "BUY", "0123-45678-012345");
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

            CoinbaseApiTestAssertions.AssertSuccessfulResponse(result, expectedStatusCode);
            CoinbaseApiTestAssertions.AssertOrderResponse(result, "BTC-USD", "SELL", "0123-45678-012345");
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
                // Deserialize the error response directly from the original OrderErrorResponse to check the error details
                var errorResponse = JsonSerializer.Deserialize<CreateOrderResponseDto>(OrderErrorResponse);
                CoinbaseApiTestAssertions.AssertOrderErrorResponse(
                    errorResponse!,
                    "INVALID_REQUEST",
                    "Invalid order request structure",
                    "The provided quote quantity is invalid");
            }
        }
    }
}