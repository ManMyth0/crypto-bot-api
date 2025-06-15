using Moq;
using System.Net;
using System.Text.Json;
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
        private IOrderMonitoringService _orderMonitoringService = null!;
        private const string TestOrderId = "test-order-123";

        [TestInitialize]
        public void TestInitialize()
        {
            _mockOrderApiClient = new Mock<ICoinbaseOrderApiClient>();
            _mockAssembleService = new Mock<IAssembleOrderDetailsService>();
            _orderMonitoringService = new OrderMonitoringService(
                _mockOrderApiClient.Object,
                _mockAssembleService.Object,
                TimeSpan.FromMilliseconds(100),  // Shorter polling interval for tests
                TimeSpan.FromSeconds(5));        // Longer timeout for tests
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

        private static JsonObject CloneOrderNode(JsonNode? node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var json = node.ToJsonString();
            var clonedNode = JsonNode.Parse(json);
            if (clonedNode == null)
            {
                throw new InvalidOperationException("Failed to parse JSON node");
            }

            return clonedNode.AsObject();
        }

        private static JsonObject CreateOrderListResponse(bool includeOrder, string status = "OPEN", string size = "1.0", string orderId = "test-order-123")
        {
            if (!includeOrder)
            {
                return CreateEmptyOrderListResponse();
            }

            var orderObject = new JsonObject
            {
                ["order_id"] = orderId,
                ["status"] = status,
                ["size"] = size,
                ["side"] = "BUY",
                ["product_id"] = "BTC-USD"
            };

            return new JsonObject
            {
                ["orders"] = new JsonArray { orderObject }
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
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

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
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

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
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

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
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

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
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

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
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

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
                async () => await _orderMonitoringService.MonitorOrderAsync(TestOrderId));
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
                _orderMonitoringService.MonitorOrderAsync(TestOrderId, cts.Token));

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
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

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
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

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
                async () => await _orderMonitoringService.MonitorOrderAsync(TestOrderId));
            
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
                async () => await _orderMonitoringService.MonitorOrderAsync(TestOrderId));
            
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
                async () => await _orderMonitoringService.MonitorOrderAsync(TestOrderId, cts.Token));
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
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

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
                async () => await _orderMonitoringService.MonitorOrderAsync(TestOrderId));
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
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

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

        [TestMethod]
        public async Task MonitorOrderAsync_RapidStateTransition_HandlesGracefully()
        {
            // Arrange
            var openOrderResponse = CreateOrderListResponse(true, "OPEN");
            var filledOrderResponse = CreateOrderListResponse(true, "FILLED");
            var fillsResponse = CreateFillsResponse(new[] { ("1.0", "2.50") });

            // Setup mock to return OPEN first, then null (order not found), then check fills
            var callCount = 0;
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1) return openOrderResponse;
                    return CreateEmptyOrderListResponse(); // Order disappeared
                });

            _mockOrderApiClient
                .Setup(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()))
                .ReturnsAsync(fillsResponse);

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "FILLED",
                Commissions = 2.50m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY"
            };

            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "FILLED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(expectedDetails);

            // Act
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual(2.50m, result.Commissions);

            // Verify the sequence of API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Exactly(2));
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_ApiReturns500Error_RetriesAndSucceeds()
        {
            // Arrange
            var openOrderResponse = CreateOrderListResponse(true, "OPEN");
            var filledOrderResponse = CreateOrderListResponse(true, "FILLED");
            var fillsResponse = CreateFillsResponse(new[] { ("1.0", "2.50") });

            var callCount = 0;
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1) return openOrderResponse;
                    if (callCount == 2) throw new CoinbaseApiException("Internal Server Error", new Exception("500 Internal Server Error"));
                    return filledOrderResponse;
                });

            _mockOrderApiClient
                .Setup(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()))
                .ReturnsAsync(fillsResponse);

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "FILLED",
                Commissions = 2.50m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY"
            };

            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "FILLED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(expectedDetails);

            // Act
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual(2.50m, result.Commissions);

            // Verify API calls - should have retried after the 500 error
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Exactly(3));
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_ApiReturnsMalformedJson_HandlesGracefully()
        {
            // Arrange
            var openOrderResponse = CreateOrderListResponse(true, "OPEN");
            var filledOrderResponse = CreateOrderListResponse(true, "FILLED");
            var fillsResponse = CreateFillsResponse(new[] { ("1.0", "2.50") });

            var callCount = 0;
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1) return openOrderResponse;
                    if (callCount == 2) throw new CoinbaseApiException("Malformed JSON response", new JsonException("Invalid JSON"));
                    return filledOrderResponse;
                });

            _mockOrderApiClient
                .Setup(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()))
                .ReturnsAsync(fillsResponse);

            var expectedDetails = new FinalizedOrderDetails
            {
                Order_Id = TestOrderId,
                Status = "FILLED",
                Commissions = 2.50m,
                Asset_Pair = "BTC-USD",
                Trade_Type = "BUY"
            };

            _mockAssembleService
                .Setup(x => x.AssembleFromFills(
                    It.Is<string>(s => s == TestOrderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "FILLED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(expectedDetails);

            // Act
            var result = await _orderMonitoringService.MonitorOrderAsync(TestOrderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual(2.50m, result.Commissions);

            // Verify API calls - should have retried after the malformed JSON
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Exactly(3));
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_InvalidStateTransition_ThrowsException()
        {
            // Arrange
            var openOrderResponse = CreateOrderListResponse(true, "OPEN");
            var invalidOrderResponse = CreateOrderListResponse(true, "INVALID_STATE");

            var callCount = 0;
            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1) return openOrderResponse;
                    return invalidOrderResponse;
                });

            // Act & Assert
            await Assert.ThrowsExceptionAsync<CoinbaseApiException>(
                async () => await _orderMonitoringService.MonitorOrderAsync(TestOrderId));

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Exactly(2));
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Never);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_MissingStatusInResponse_ThrowsException()
        {
            // Arrange
            var orderResponse = new JsonObject
            {
                ["orders"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["order_id"] = TestOrderId,
                        // Missing status field
                        ["size"] = "1.0",
                        ["side"] = "BUY",
                        ["product_id"] = "BTC-USD"
                    }
                }
            };

            _mockOrderApiClient
                .Setup(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()))
                .ReturnsAsync(orderResponse);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<CoinbaseApiException>(
                async () => await _orderMonitoringService.MonitorOrderAsync(TestOrderId));

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.IsAny<ListOrdersRequestDto>()), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.IsAny<ListOrderFillsRequestDto>()), Times.Never);
        }

        [TestMethod]
        public async Task MonitorMultipleOrdersAsync_HandlesSuccessfully()
        {
            // Arrange
            var order1Id = "test-order-1";
            var order2Id = "test-order-2";
            var order3Id = "test-order-3";

            // Setup order responses
            _mockOrderApiClient.Setup(x => x.ListOrdersAsync(It.Is<ListOrdersRequestDto>(r => r.OrderIds != null && r.OrderIds.Contains(order1Id))))
                .ReturnsAsync(new JsonObject
                {
                    ["orders"] = new JsonArray
                    {
                        CloneOrderNode(CreateOrderListResponse(true, "FILLED", "1.0", order1Id)["orders"]?[0])
                    }
                });

            _mockOrderApiClient.Setup(x => x.ListOrdersAsync(It.Is<ListOrdersRequestDto>(r => r.OrderIds != null && r.OrderIds.Contains(order2Id))))
                .ReturnsAsync(new JsonObject
                {
                    ["orders"] = new JsonArray
                    {
                        CloneOrderNode(CreateOrderListResponse(true, "FILLED", "1.0", order2Id)["orders"]?[0])
                    }
                });

            _mockOrderApiClient.Setup(x => x.ListOrdersAsync(It.Is<ListOrdersRequestDto>(r => r.OrderIds != null && r.OrderIds.Contains(order3Id))))
                .ReturnsAsync(new JsonObject
                {
                    ["orders"] = new JsonArray
                    {
                        CloneOrderNode(CreateOrderListResponse(true, "FILLED", "1.0", order3Id)["orders"]?[0])
                    }
                });

            // Setup order-specific fills responses
            _mockOrderApiClient.Setup(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order1Id)))
                .ReturnsAsync(new JsonObject
                {
                    ["fills"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["order_id"] = order1Id,
                            ["size"] = "1.0",
                            ["price"] = "50000.0",
                            ["commission"] = "0.1",
                            ["time"] = DateTime.UtcNow.ToString("O")
                        }
                    }
                });

            _mockOrderApiClient.Setup(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order2Id)))
                .ReturnsAsync(new JsonObject
                {
                    ["fills"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["order_id"] = order2Id,
                            ["size"] = "1.0",
                            ["price"] = "51000.0",
                            ["commission"] = "0.2",
                            ["time"] = DateTime.UtcNow.ToString("O")
                        }
                    }
                });

            _mockOrderApiClient.Setup(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order3Id)))
                .ReturnsAsync(new JsonObject
                {
                    ["fills"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["order_id"] = order3Id,
                            ["size"] = "1.0",
                            ["price"] = "52000.0",
                            ["commission"] = "0.3",
                            ["time"] = DateTime.UtcNow.ToString("O")
                        }
                    }
                });

            // Setup AssembleService to return order-specific details
            _mockAssembleService.Setup(x => x.AssembleFromFills(
                    It.Is<string>(id => id == order1Id),
                    It.IsAny<JsonArray>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal?>()))
                .Returns(new FinalizedOrderDetails
                {
                    Order_Id = order1Id,
                    Status = "FILLED",
                    Initial_Size = 1.0m,
                    Acquired_Price = 50000.0m,
                    Commissions = 0.1m
                });

            _mockAssembleService.Setup(x => x.AssembleFromFills(
                    It.Is<string>(id => id == order2Id),
                    It.IsAny<JsonArray>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal?>()))
                .Returns(new FinalizedOrderDetails
                {
                    Order_Id = order2Id,
                    Status = "FILLED",
                    Initial_Size = 1.0m,
                    Acquired_Price = 51000.0m,
                    Commissions = 0.2m
                });

            _mockAssembleService.Setup(x => x.AssembleFromFills(
                    It.Is<string>(id => id == order3Id),
                    It.IsAny<JsonArray>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal?>()))
                .Returns(new FinalizedOrderDetails
                {
                    Order_Id = order3Id,
                    Status = "FILLED",
                    Initial_Size = 1.0m,
                    Acquired_Price = 52000.0m,
                    Commissions = 0.3m
                });

            // Act
            var tasks = new[]
            {
                _orderMonitoringService.MonitorOrderAsync(order1Id),
                _orderMonitoringService.MonitorOrderAsync(order2Id),
                _orderMonitoringService.MonitorOrderAsync(order3Id)
            };

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(3, results.Length);
            
            // Order 1 assertions
            var result1 = results[0];
            Assert.IsNotNull(result1);
            Assert.AreEqual(order1Id, result1.Order_Id);
            Assert.AreEqual("FILLED", result1.Status);
            Assert.AreEqual(0.1m, result1.Commissions);
            Assert.AreEqual(50000.0m, result1.Acquired_Price);

            // Order 2 assertions
            var result2 = results[1];
            Assert.IsNotNull(result2);
            Assert.AreEqual(order2Id, result2.Order_Id);
            Assert.AreEqual("FILLED", result2.Status);
            Assert.AreEqual(0.2m, result2.Commissions);
            Assert.AreEqual(51000.0m, result2.Acquired_Price);

            // Order 3 assertions
            var result3 = results[2];
            Assert.IsNotNull(result3);
            Assert.AreEqual(order3Id, result3.Order_Id);
            Assert.AreEqual("FILLED", result3.Status);
            Assert.AreEqual(0.3m, result3.Commissions);
            Assert.AreEqual(52000.0m, result3.Acquired_Price);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order1Id)), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order2Id)), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order3Id)), Times.Once);
        }

        [TestMethod]
        public async Task MonitorMultipleOrdersAsync_SomeOrdersFail_OthersSucceed()
        {
            // Arrange
            var order1Id = "test-order-1";
            var order2Id = "test-order-2";
            var order3Id = "test-order-3";

            // Setup successful order responses
            _mockOrderApiClient.Setup(x => x.ListOrdersAsync(It.Is<ListOrdersRequestDto>(r => r.OrderIds != null && r.OrderIds.Contains(order1Id))))
                .ReturnsAsync(new JsonObject
                {
                    ["orders"] = new JsonArray
                    {
                        CloneOrderNode(CreateOrderListResponse(true, "FILLED", "1.0", order1Id)["orders"]?[0])
                    }
                });

            // Setup failing order
            _mockOrderApiClient.Setup(x => x.ListOrdersAsync(It.Is<ListOrdersRequestDto>(r => r.OrderIds != null && r.OrderIds.Contains(order2Id))))
                .ThrowsAsync(new CoinbaseApiException("Order not found"));

            _mockOrderApiClient.Setup(x => x.ListOrdersAsync(It.Is<ListOrdersRequestDto>(r => r.OrderIds != null && r.OrderIds.Contains(order3Id))))
                .ReturnsAsync(new JsonObject
                {
                    ["orders"] = new JsonArray
                    {
                        CloneOrderNode(CreateOrderListResponse(true, "FILLED", "1.0", order3Id)["orders"]?[0])
                    }
                });

            // Setup fills responses for successful orders
            _mockOrderApiClient.Setup(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order1Id)))
                .ReturnsAsync(new JsonObject
                {
                    ["fills"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["order_id"] = order1Id,
                            ["size"] = "1.0",
                            ["price"] = "50000.0",
                            ["commission"] = "0.1",
                            ["time"] = DateTime.UtcNow.ToString("O")
                        }
                    }
                });

            _mockOrderApiClient.Setup(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order3Id)))
                .ReturnsAsync(new JsonObject
                {
                    ["fills"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["order_id"] = order3Id,
                            ["size"] = "1.0",
                            ["price"] = "52000.0",
                            ["commission"] = "0.3",
                            ["time"] = DateTime.UtcNow.ToString("O")
                        }
                    }
                });

            // Setup AssembleService for successful orders
            _mockAssembleService.Setup(x => x.AssembleFromFills(
                    It.Is<string>(id => id == order1Id),
                    It.IsAny<JsonArray>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal?>()))
                .Returns(new FinalizedOrderDetails
                {
                    Order_Id = order1Id,
                    Status = "FILLED",
                    Initial_Size = 1.0m,
                    Acquired_Price = 50000.0m,
                    Commissions = 0.1m
                });

            _mockAssembleService.Setup(x => x.AssembleFromFills(
                    It.Is<string>(id => id == order3Id),
                    It.IsAny<JsonArray>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal?>()))
                .Returns(new FinalizedOrderDetails
                {
                    Order_Id = order3Id,
                    Status = "FILLED",
                    Initial_Size = 1.0m,
                    Acquired_Price = 52000.0m,
                    Commissions = 0.3m
                });

            // Act & Assert
            var tasks = new Task<FinalizedOrderDetails?>[]
            {
                _orderMonitoringService.MonitorOrderAsync(order1Id),
                Task.Run(async () => {
                    try
                    {
                        return await _orderMonitoringService.MonitorOrderAsync(order2Id);
                    }
                    catch (CoinbaseApiException)
                    {
                        return null;
                    }
                }),
                _orderMonitoringService.MonitorOrderAsync(order3Id)
            };

            var results = await Task.WhenAll(tasks);

            // Assert successful orders
            Assert.IsNotNull(results[0], "First order should succeed");
            Assert.AreEqual(order1Id, results[0]!.Order_Id);
            Assert.AreEqual("FILLED", results[0]!.Status);
            Assert.AreEqual(0.1m, results[0]!.Commissions);
            Assert.AreEqual(50000.0m, results[0]!.Acquired_Price);

            // Assert failed order
            Assert.IsNull(results[1], "Second order should fail");

            // Assert successful order
            Assert.IsNotNull(results[2], "Third order should succeed");
            Assert.AreEqual(order3Id, results[2]!.Order_Id);
            Assert.AreEqual("FILLED", results[2]!.Status);
            Assert.AreEqual(0.3m, results[2]!.Commissions);
            Assert.AreEqual(52000.0m, results[2]!.Acquired_Price);

            // Verify API calls
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order1Id)), Times.Once);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order2Id)), Times.Never);
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == order3Id)), Times.Once);
        }

        [TestMethod]
        public async Task MonitorOrderAsync_WhenNetworkTimeout_ShouldRetryAndSucceed()
        {
            // Arrange
            var orderId = "test-order-timeout";
            var callCount = 0;
            var maxRetries = 3;

            // Setup order response with timeouts and eventual success
            _mockOrderApiClient.Setup(x => x.ListOrdersAsync(It.Is<ListOrdersRequestDto>(r => r.OrderIds != null && r.OrderIds.Contains(orderId))))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount <= maxRetries)
                    {
                        throw new TimeoutException("Network request timed out");
                    }
                    return Task.FromResult(new JsonObject
                    {
                        ["orders"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["order_id"] = orderId,
                                ["status"] = "FILLED",
                                ["size"] = "1.0",
                                ["side"] = "BUY",
                                ["product_id"] = "BTC-USD"
                            }
                        }
                    });
                });

            // Setup fills response
            _mockOrderApiClient.Setup(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == orderId)))
                .ReturnsAsync(new JsonObject
                {
                    ["fills"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["order_id"] = orderId,
                            ["size"] = "1.0",
                            ["price"] = "50000.0",
                            ["commission"] = "0.1",
                            ["time"] = DateTime.UtcNow.ToString("O")
                        }
                    }
                });

            // Setup AssembleService
            _mockAssembleService.Setup(x => x.AssembleFromFills(
                    It.Is<string>(id => id == orderId),
                    It.IsAny<JsonArray>(),
                    It.Is<string>(s => s == "FILLED"),
                    It.Is<decimal?>(d => d == 1.0m)))
                .Returns(new FinalizedOrderDetails
                {
                    Order_Id = orderId,
                    Status = "FILLED",
                    Initial_Size = 1.0m,
                    Acquired_Price = 50000.0m,
                    Commissions = 0.1m
                });

            // Act
            var result = await _orderMonitoringService.MonitorOrderAsync(orderId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(orderId, result.Order_Id);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual(1.0m, result.Initial_Size);
            Assert.AreEqual(50000.0m, result.Acquired_Price);
            Assert.AreEqual(0.1m, result.Commissions);

            // Verify retry behavior
            Assert.AreEqual(maxRetries + 1, callCount, "Should have retried exactly 3 times before succeeding");
            _mockOrderApiClient.Verify(x => x.ListOrdersAsync(It.Is<ListOrdersRequestDto>(r => r.OrderIds != null && r.OrderIds.Contains(orderId))), Times.Exactly(maxRetries + 1));
            _mockOrderApiClient.Verify(x => x.ListOrderFillsAsync(It.Is<ListOrderFillsRequestDto>(r => r.OrderId == orderId)), Times.Once);
        }
    }
} 