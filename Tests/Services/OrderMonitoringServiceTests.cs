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
        private Mock<IAssembleOrderDetailsService> _mockAssembleService = null!;
        private IOrderMonitoringService _monitoringService = null!;
        private const string TestOrderId = "test-order-123";

        [TestInitialize]
        public void TestInitialize()
        {
            _mockOrderApiClient = new Mock<ICoinbaseOrderApiClient>();
            _mockAssembleService = new Mock<IAssembleOrderDetailsService>();
            _monitoringService = new OrderMonitoringService(
                _mockOrderApiClient.Object,
                _mockAssembleService.Object,
                TimeSpan.FromMilliseconds(1),  // Fast polling for tests
                TimeSpan.FromSeconds(1));      // Reasonable timeout for tests
        }

        private void SetupMockResponses(JsonObject orderListResponse, JsonObject fillsResponse)
        {
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ReturnsAsync(orderListResponse);

            _mockOrderApiClient
                .Setup(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()))
                .ReturnsAsync(fillsResponse);
        }

        private static JsonObject CreateOrderListResponse(bool includeOrder, string status = "OPEN", string size = "1.0")
        {
            if (!includeOrder)
            {
                return CreateEmptyOrderListResponse();
            }

            return new JsonObject
            {
                ["orders"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["order_id"] = TestOrderId,
                        ["status"] = status,
                        ["size"] = size,
                        ["side"] = "BUY",
                        ["product_id"] = "BTC-USD"
                    }
                }
            };
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
                    ["trade_id"] = Guid.NewGuid().ToString(),
                    ["size"] = size,
                    ["commission"] = commission,
                    ["side"] = "BUY",
                    ["product_id"] = "BTC-USD",
                    ["trade_time"] = DateTime.UtcNow.ToString("O")
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

        [TestMethod]
        public async Task MonitorOrderAsync_OrderFilledWithMultipleFills_ReturnsFinalizedDetails()
        {
            // Arrange
            var openOrderResponse = CreateOrderListResponse(true);  // First check: order exists
            var filledOrderResponse = CreateOrderListResponse(true, "FILLED");  // Second check: order is filled

            var fillsResponse = CreateFillsResponse(new[]
            {
                ("0.5", "1.25"),    // First fill: size 0.5, commission 1.25
                ("0.3", "0.75"),    // Second fill: size 0.3, commission 0.75
                ("0.2", "0.50")     // Third fill: size 0.2, commission 0.50
            });

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "FILLED",
                Commissions = 2.50m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY"
            };

            // Setup mock to return OPEN first, then FILLED
            var callCount = 0;
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ReturnsAsync(() => callCount++ == 0 ? openOrderResponse : filledOrderResponse);

            _mockOrderApiClient
                .Setup(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()))
                .ReturnsAsync(fillsResponse);

            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "FILLED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(expectedDetails);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual(2.50m, result.Commissions);
            Assert.AreEqual("BTC-USD", result.Asset_Pair);
            Assert.AreEqual("BUY", result.Trade_Type);

            // Verify number of calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Exactly(2));
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderCancelledWithPartialFills_ReturnsCancelledStatusAndCommissions()
        {
            // Arrange
            var orderResponse = CreateOrderListResponse(true, "CANCELLED");

            var fillsResponse = CreateFillsResponse(new[]
            {
                ("0.3", "0.75"),    // Partial fill before cancellation
                ("0.2", "0.50")     // Another partial fill
            });

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "CANCELLED",
                Commissions = 1.25m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY"
            };

            SetupMockResponses(orderResponse, fillsResponse);
            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "CANCELLED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(expectedDetails);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("CANCELLED", result.Status);
            Assert.AreEqual(1.25m, result.Commissions);
            Assert.AreEqual("BTC-USD", result.Asset_Pair);
            Assert.AreEqual("BUY", result.Trade_Type);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderCancelledWithNoFills_ReturnsCancelledStatus()
        {
            // Arrange
            var orderResponse = CreateOrderListResponse(true, "CANCELLED");

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "CANCELLED",
                Commissions = 0m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY"
            };

            SetupMockResponses(orderResponse, CreateEmptyFillsResponse());
            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "CANCELLED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(expectedDetails);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("CANCELLED", result.Status);
            Assert.AreEqual(0m, result.Commissions);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderExpiredWithPartialFills_ReturnsExpiredStatusAndCommissions()
        {
            // Arrange
            var orderResponse = CreateOrderListResponse(true, "EXPIRED");

            var fillsResponse = CreateFillsResponse(new[]
            {
                ("0.3", "0.75"),    // Partial fill before expiration
                ("0.2", "0.50")     // Another partial fill
            });

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "EXPIRED",
                Commissions = 1.25m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY"
            };

            SetupMockResponses(orderResponse, fillsResponse);
            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "EXPIRED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(expectedDetails);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("EXPIRED", result.Status);
            Assert.AreEqual(1.25m, result.Commissions);
            Assert.AreEqual("BTC-USD", result.Asset_Pair);
            Assert.AreEqual("BUY", result.Trade_Type);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderExpiredWithNoFills_ReturnsExpiredStatus()
        {
            // Arrange
            var orderResponse = CreateOrderListResponse(true, "EXPIRED");

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "EXPIRED",
                Commissions = 0m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY"
            };

            SetupMockResponses(orderResponse, CreateEmptyFillsResponse());
            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "EXPIRED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(expectedDetails);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("EXPIRED", result.Status);
            Assert.AreEqual(0m, result.Commissions);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderRejected_ReturnsRejectedStatus()
        {
            // Arrange
            var orderResponse = CreateOrderListResponse(true, "REJECTED");

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "REJECTED",
                Commissions = 0m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY"
            };

            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ReturnsAsync(orderResponse);

            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.Is<JsonArray>(a => a.Count == 0),
                    It.Is<string>(s => s == "REJECTED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(expectedDetails);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("REJECTED", result.Status);
            Assert.AreEqual(0m, result.Commissions);

            // Verify API calls - should not check fills for rejected orders
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Never);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_ApiError_ThrowsCoinbaseApiException()
        {
            // Arrange
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ThrowsAsync(new CoinbaseApiException("API Error", new Exception("Internal Server Error")));

            // Act & Assert
            await Assert.ThrowsExceptionAsync<CoinbaseApiException>(
                async () => await _monitoringService.MonitorOrderAsync(TestOrderId));
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

        [TestMethod]
        public async Task MonitorOrderAsync_OrderCompletedBeforeFirstCheck_ReturnsFinalizedDetails()
        {
            // Arrange
            var orderResponse = CreateOrderListResponse(true, "FILLED");
            var fillsResponse = CreateFillsResponse(new[] { ("1.0", "2.50") });

            SetupMockResponses(orderResponse, fillsResponse);
            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "FILLED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(new FinalizedOrderDetails
                {
                    Order_Id = TestOrderId,
                    Status = "FILLED",
                    Commissions = 2.50m,
                    Asset_Pair = "BTC-USD",
                    Trade_Type = "BUY"
                });

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual(2.50m, result.Commissions);
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderPartiallyFilledThenCancelled_ReturnsFinalizedDetails()
        {
            // Arrange
            var orderResponse = CreateOrderListResponse(true, "CANCELLED");

            var fillsResponse = CreateFillsResponse(new[]
            {
                ("0.5", "1.25")    // Partial fill before cancellation
            });

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "CANCELLED",
                Commissions = 1.25m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY"
            };

            SetupMockResponses(orderResponse, fillsResponse);
            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "CANCELLED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(expectedDetails);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("CANCELLED", result.Status);
            Assert.AreEqual(1.25m, result.Commissions);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_ExceedsTimeout_ThrowsTimeoutException()
        {
            // Arrange
            var orderResponse = CreateOrderListResponse(true, "OPEN");
            SetupMockResponses(orderResponse, CreateEmptyFillsResponse());

            var service = new OrderMonitoringService(
                _mockOrderApiClient.Object,
                _mockAssembleService.Object,
                TimeSpan.FromMilliseconds(1),  // Very short polling interval
                TimeSpan.FromMilliseconds(1));  // Immediate timeout

            // Act & Assert
            var ex = await Assert.ThrowsExceptionAsync<TimeoutException>(
                async () => await service.MonitorOrderAsync(TestOrderId));
            
            Assert.IsTrue(ex.Message.Contains("exceeded timeout"));
        }

        [TestMethod]
        public async Task MonitorOrderAsync_GTDOrderWithEndTime_RespectsEndTime()
        {
            // Arrange
            var endTime = DateTime.UtcNow.AddMilliseconds(100); // Very short timeout
            var orderResponse = new JsonObject
            {
                ["orders"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["order_id"] = TestOrderId,
                        ["status"] = "OPEN",
                        ["size"] = "1.5",
                        ["side"] = "BUY",
                        ["product_id"] = "BTC-USD",
                        ["end_time"] = endTime.ToString("O")
                    }
                }
            };

            SetupMockResponses(orderResponse, CreateEmptyFillsResponse());

            // Act & Assert
            var ex = await Assert.ThrowsExceptionAsync<TimeoutException>(
                async () => await _monitoringService.MonitorOrderAsync(TestOrderId));
            
            Assert.IsTrue(ex.Message.Contains("GTD order"));
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_GTDOrderWithPastEndTime_ThrowsTimeoutImmediately()
        {
            // Arrange
            var pastEndTime = DateTime.UtcNow.AddMinutes(-5);
            var orderResponse = new JsonObject
            {
                ["orders"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["order_id"] = TestOrderId,
                        ["status"] = "OPEN",
                        ["size"] = "1.5",
                        ["side"] = "BUY",
                        ["product_id"] = "BTC-USD",
                        ["end_time"] = pastEndTime.ToString("O")
                    }
                }
            };

            SetupMockResponses(orderResponse, CreateEmptyFillsResponse());

            // Act & Assert
            var ex = await Assert.ThrowsExceptionAsync<TimeoutException>(
                async () => await _monitoringService.MonitorOrderAsync(TestOrderId));
            
            Assert.IsTrue(ex.Message.Contains("already expired"));
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_UserCancellation_PropagatesCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var orderResponse = CreateOrderListResponse(true, "OPEN");
            SetupMockResponses(orderResponse, CreateEmptyFillsResponse());

            // Cancel immediately
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                async () => await _monitoringService.MonitorOrderAsync(TestOrderId, cts.Token));
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderInTerminalStateImmediately_ReturnsWithoutMonitoring()
        {
            // Arrange
            var orderResponse = CreateOrderListResponse(true, "FILLED");
            var fillsResponse = CreateFillsResponse(new[] { ("1.0", "2.50") });

            SetupMockResponses(orderResponse, fillsResponse);
            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "FILLED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(new FinalizedOrderDetails
                {
                    Order_Id = TestOrderId,
                    Status = "FILLED",
                    Commissions = 2.50m
                });

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual(2.50m, result.Commissions);
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_OrderNotFound_ThrowsException()
        {
            // Arrange
            var emptyResponse = new JsonObject { ["orders"] = new JsonArray() };
            SetupMockResponses(emptyResponse, CreateEmptyFillsResponse());

            // Act & Assert
            await Assert.ThrowsExceptionAsync<CoinbaseApiException>(
                async () => await _monitoringService.MonitorOrderAsync(TestOrderId));
        }

        [TestMethod]
        public async Task MonitorOrderAsync_InvalidSizeFormat_HandlesGracefully()
        {
            // Arrange
            var openOrderResponse = new JsonObject
            {
                ["orders"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["order_id"] = TestOrderId,
                        ["status"] = "OPEN",
                        ["size"] = "invalid",
                        ["side"] = "BUY",
                        ["product_id"] = "BTC-USD"
                    }
                }
            };

            var filledOrderResponse = new JsonObject
            {
                ["orders"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["order_id"] = TestOrderId,
                        ["status"] = "FILLED",
                        ["size"] = "invalid",
                        ["side"] = "BUY",
                        ["product_id"] = "BTC-USD"
                    }
                }
            };

            var fillsResponse = CreateFillsResponse(new[] { ("1.0", "2.50") });

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "FILLED",
                Commissions = 2.50m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY",
                Initial_Size = null  // Should be null due to invalid size format
            };

            // Setup mock to return OPEN first, then FILLED
            var callCount = 0;
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ReturnsAsync(() => callCount++ == 0 ? openOrderResponse : filledOrderResponse);

            _mockOrderApiClient
                .Setup(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()))
                .ReturnsAsync(fillsResponse);

            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "FILLED"),
                    It.Is<decimal?>(d => d == null)))  // Expect null size
                .Returns(expectedDetails);

            // Act
            var result = await _monitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual(2.50m, result.Commissions);
            Assert.IsNull(result.Initial_Size);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Exactly(2));
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }
    }
} 