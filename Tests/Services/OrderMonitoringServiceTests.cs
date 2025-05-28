using Moq;
using System.Net;
using System.Text.Json.Nodes;
using crypto_bot_api.Services;
using crypto_bot_api.Models;
using crypto_bot_api.Tests.Utilities;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class OrderMonitoringServiceTests
    {
        private Mock<ICoinbaseOrderApiClient> _mockOrderApiClient = null!;
        private IOrderMonitoringService _monitoringService = null!;
        private const string TestOrderId = "test-order-123";

        [TestInitialize]
        public void TestInitialize()
        {
            _mockOrderApiClient = new Mock<ICoinbaseOrderApiClient>();
            _monitoringService = new OrderMonitoringService(
                _mockOrderApiClient.Object,
                TimeSpan.Zero);  // No delay for tests
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderFilledWithMultipleFills_ReturnsFinalizedDetails()
        {
            // Arrange
            var orderListResponses = new Queue<JsonObject>(new[]
            {
                CreateOrderListResponse(true),  // First check: order exists
                CreateOrderListResponse(false)  // Second check: order gone
            });

            var fillsResponse = CreateFillsResponse(new[]
            {
                ("0.5", "1.25"),    // First fill: size 0.5, commission 1.25
                ("0.3", "0.75"),    // Second fill: size 0.3, commission 0.75
                ("0.2", "0.50")     // Third fill: size 0.2, commission 0.50
            });

            SetupMockResponses(orderListResponses, fillsResponse);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.OrderId);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual(2.50m, result.TotalCommission); // Sum of all commissions
            Assert.AreEqual("BTC-USD", result.ProductId);
            Assert.AreEqual("BUY", result.Side);

            // Verify number of calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Exactly(2));
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderCancelledWithPartialFills_ReturnsCancelledStatusAndCommissions()
        {
            // Arrange
            var orderListResponses = new Queue<JsonObject>(new[]
            {
                CreateOrderListResponse(true, "CANCELLED")
            });

            var fillsResponse = CreateFillsResponse(new[]
            {
                ("0.3", "0.75"),    // Partial fill before cancellation
                ("0.2", "0.50")     // Another partial fill
            });

            SetupMockResponses(orderListResponses, fillsResponse);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.OrderId);
            Assert.AreEqual("CANCELLED", result.Status);
            Assert.AreEqual(1.25m, result.TotalCommission); // Sum of partial fill commissions
            Assert.AreEqual("BTC-USD", result.ProductId);
            Assert.AreEqual("BUY", result.Side);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderCancelledWithNoFills_ReturnsCancelledStatus()
        {
            // Arrange
            var orderListResponses = new Queue<JsonObject>(new[]
            {
                CreateOrderListResponse(true, "CANCELLED")
            });

            SetupMockResponses(orderListResponses, CreateEmptyFillsResponse());

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.OrderId);
            Assert.AreEqual("CANCELLED", result.Status);
            Assert.AreEqual(0m, result.TotalCommission);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderExpiredWithPartialFills_ReturnsExpiredStatusAndCommissions()
        {
            // Arrange
            var orderListResponses = new Queue<JsonObject>(new[]
            {
                CreateOrderListResponse(true, "EXPIRED")
            });

            var fillsResponse = CreateFillsResponse(new[]
            {
                ("0.3", "0.75"),    // Partial fill before expiration
                ("0.2", "0.50")     // Another partial fill
            });

            SetupMockResponses(orderListResponses, fillsResponse);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.OrderId);
            Assert.AreEqual("EXPIRED", result.Status);
            Assert.AreEqual(1.25m, result.TotalCommission); // Sum of partial fill commissions
            Assert.AreEqual("BTC-USD", result.ProductId);
            Assert.AreEqual("BUY", result.Side);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderExpiredWithNoFills_ReturnsExpiredStatus()
        {
            // Arrange
            var orderListResponses = new Queue<JsonObject>(new[]
            {
                CreateOrderListResponse(true, "EXPIRED")
            });

            SetupMockResponses(orderListResponses, CreateEmptyFillsResponse());

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.OrderId);
            Assert.AreEqual("EXPIRED", result.Status);
            Assert.AreEqual(0m, result.TotalCommission);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderRejected_ReturnsRejectedStatus()
        {
            // Arrange
            var orderListResponses = new Queue<JsonObject>(new[]
            {
                CreateOrderListResponse(true, "REJECTED")
            });

            SetupMockResponses(orderListResponses, CreateEmptyFillsResponse());

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.OrderId);
            Assert.AreEqual("REJECTED", result.Status);
            Assert.AreEqual(0m, result.TotalCommission);

            // Verify API calls - should never check fills for REJECTED orders
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Never);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_ApiError_ThrowsCoinbaseApiException()
        {
            // Arrange
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ThrowsAsync(new CoinbaseApiException("API Error"));

            // Act & Assert
            await Assert.ThrowsExceptionAsync<CoinbaseApiException>(() =>
                _monitoringService.MonitorOrderAsync(TestOrderId));

            // Verify number of calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Never);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_Cancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .Callback(() => cts.Cancel())
                .ReturnsAsync(CreateOrderListResponse(true));

            // Act & Assert
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
                _monitoringService.MonitorOrderAsync(TestOrderId, cts.Token));

            // Verify number of calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Never);
        }

        private void SetupMockResponses(Queue<JsonObject> orderResponses, JsonObject fillsResponse)
        {
            var callCount = 0;
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return orderResponses.Count > 0 ? orderResponses.Dequeue() : CreateEmptyOrderListResponse();
                });

            _mockOrderApiClient
                .Setup(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()))
                .ReturnsAsync(fillsResponse);
        }

        private static JsonObject CreateOrderListResponse(bool includeOrder, string status = "OPEN")
        {
            var response = new JsonObject
            {
                ["orders"] = new JsonArray()
            };

            if (includeOrder)
            {
                ((JsonArray)response["orders"]!).Add(new JsonObject
                {
                    ["order_id"] = TestOrderId,
                    ["status"] = status,
                    ["product_id"] = "BTC-USD",
                    ["side"] = "BUY"
                });
            }

            return response;
        }

        private static JsonObject CreateEmptyOrderListResponse()
        {
            return new JsonObject
            {
                ["orders"] = new JsonArray()
            };
        }

        private static JsonObject CreateFillsResponse(IEnumerable<(string Size, string Commission)> fills)
        {
            var fillsArray = new JsonArray();

            foreach (var (size, commission) in fills)
            {
                fillsArray.Add(new JsonObject
                {
                    ["entry_id"] = "entry-123",
                    ["trade_id"] = "trade-123",
                    ["order_id"] = TestOrderId,
                    ["trade_time"] = DateTime.UtcNow.ToString("O"),
                    ["trade_type"] = "FILL",
                    ["price"] = "50000.00",
                    ["size"] = size,
                    ["commission"] = commission,
                    ["product_id"] = "BTC-USD",
                    ["sequence_timestamp"] = DateTime.UtcNow.ToString("O"),
                    ["liquidity_indicator"] = "TAKER",
                    ["size_in_quote"] = false,
                    ["user_id"] = "user-123",
                    ["side"] = "BUY",
                    ["retail_portfolio_id"] = "portfolio-123"
                });
            }

            return new JsonObject
            {
                ["fills"] = fillsArray
            };
        }

        private static JsonObject CreateEmptyFillsResponse()
        {
            return new JsonObject
            {
                ["fills"] = new JsonArray()
            };
        }
    }
} 