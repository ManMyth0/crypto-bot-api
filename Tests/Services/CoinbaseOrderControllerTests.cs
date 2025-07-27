using crypto_bot_api.Controllers;
using crypto_bot_api.Models.DTOs.Orders;
using crypto_bot_api.Models;
using crypto_bot_api.Services;
using crypto_bot_api.CustomExceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json.Nodes;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class CoinbaseOrderControllerTests
    {
        private Mock<ICoinbaseOrderApiClient> _mockCoinbaseOrderClient = null!;
        private Mock<IAssembleOrderDetailsService> _mockAssembleOrderDetails = null!;
        private Mock<IPositionManagementService> _mockPositionManager = null!;
        private Mock<IOrderValidationService> _mockOrderValidation = null!;
        private Mock<IOrderMonitoringService> _mockOrderMonitoringService = null!;
        private Mock<ILogger<CoinbaseOrderController>> _mockLogger = null!;
        private Mock<IServiceScopeFactory> _mockServiceScopeFactory = null!;
        private Mock<IServiceScope> _mockServiceScope = null!;
        private Mock<IServiceProvider> _mockServiceProvider = null!;
        private CoinbaseOrderController _controller = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockCoinbaseOrderClient = new Mock<ICoinbaseOrderApiClient>();
            _mockAssembleOrderDetails = new Mock<IAssembleOrderDetailsService>();
            _mockPositionManager = new Mock<IPositionManagementService>();
            _mockOrderValidation = new Mock<IOrderValidationService>();
            _mockOrderMonitoringService = new Mock<IOrderMonitoringService>();
            _mockLogger = new Mock<ILogger<CoinbaseOrderController>>();
            _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
            _mockServiceScope = new Mock<IServiceScope>();
            _mockServiceProvider = new Mock<IServiceProvider>();

            _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);
            _mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
            _mockServiceScope.Setup(x => x.Dispose());

            _controller = new CoinbaseOrderController(
                _mockCoinbaseOrderClient.Object,
                _mockAssembleOrderDetails.Object,
                _mockPositionManager.Object,
                _mockOrderValidation.Object,
                _mockOrderMonitoringService.Object,
                _mockLogger.Object,
                _mockServiceScopeFactory.Object
            );
        }

        [TestMethod]
        public async Task CreateOrder_WithNullPositionType_ReturnsSuccessResponse()
        {
            // Arrange
            var orderRequest = new CreateOrderRequestDto
            {
                ClientOrderId = "test-order-123",
                ProductId = "BTC-USD",
                Side = "BUY",
                PositionType = null, // Explicitly omit position_type
                OrderConfiguration = new OrderConfigurationDto
                {
                    LimitLimitGtc = new LimitLimitGtcDto
                    {
                        BaseSize = "0.001",
                        LimitPrice = "50000.00"
                    }
                }
            };

            var validationResult = new ValidationResult
            {
                IsValid = true,
                Warnings = new List<string>()
            };

            var coinbaseResponse = new JsonObject
            {
                ["order_id"] = "coinbase-order-123"
            };

            _mockOrderValidation.Setup(x => x.ValidateOrderAsync(It.IsAny<CreateOrderRequestDto>()))
                .ReturnsAsync(validationResult);

            _mockCoinbaseOrderClient.Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()))
                .ReturnsAsync(coinbaseResponse);

            // Act
            var result = await _controller.CreateOrder(orderRequest);

            // Assert
            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
            
            var okResult = result.Result as OkObjectResult;
            Assert.IsNotNull(okResult);
            
            var orderResponse = okResult.Value as OrderResponse;
            Assert.IsNotNull(orderResponse);
            Assert.AreEqual("", orderResponse.PositionType); // Should be empty string when null
            
            // Verify that order validation was called
            _mockOrderValidation.Verify(x => x.ValidateOrderAsync(It.IsAny<CreateOrderRequestDto>()), Times.Once);
            
            // Verify that Coinbase order creation was called
            _mockCoinbaseOrderClient.Verify(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task CreateOrder_WithEmptyPositionType_ReturnsSuccessResponse()
        {
            // Arrange
            var orderRequest = new CreateOrderRequestDto
            {
                ClientOrderId = "test-order-123",
                ProductId = "BTC-USD",
                Side = "BUY",
                PositionType = "", // Explicitly empty position_type
                OrderConfiguration = new OrderConfigurationDto
                {
                    LimitLimitGtc = new LimitLimitGtcDto
                    {
                        BaseSize = "0.001",
                        LimitPrice = "50000.00"
                    }
                }
            };

            var validationResult = new ValidationResult
            {
                IsValid = true,
                Warnings = new List<string>()
            };

            var coinbaseResponse = new JsonObject
            {
                ["order_id"] = "coinbase-order-123"
            };

            _mockOrderValidation.Setup(x => x.ValidateOrderAsync(It.IsAny<CreateOrderRequestDto>()))
                .ReturnsAsync(validationResult);

            _mockCoinbaseOrderClient.Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()))
                .ReturnsAsync(coinbaseResponse);

            // Act
            var result = await _controller.CreateOrder(orderRequest);

            // Assert
            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
            
            var okResult = result.Result as OkObjectResult;
            Assert.IsNotNull(okResult);
            
            var orderResponse = okResult.Value as OrderResponse;
            Assert.IsNotNull(orderResponse);
            Assert.AreEqual("", orderResponse.PositionType); // Should be empty string when empty
            
            // Verify that order validation was called
            _mockOrderValidation.Verify(x => x.ValidateOrderAsync(It.IsAny<CreateOrderRequestDto>()), Times.Once);
            
            // Verify that Coinbase order creation was called
            _mockCoinbaseOrderClient.Verify(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()), Times.Once);
        }

        [TestMethod]
        public async Task CreateOrder_WithValidPositionType_ReturnsSuccessResponse()
        {
            // Arrange
            var orderRequest = new CreateOrderRequestDto
            {
                ClientOrderId = "test-order-123",
                ProductId = "BTC-USD",
                Side = "BUY",
                PositionType = "LONG", // Valid position_type
                OrderConfiguration = new OrderConfigurationDto
                {
                    LimitLimitGtc = new LimitLimitGtcDto
                    {
                        BaseSize = "0.001",
                        LimitPrice = "50000.00"
                    }
                }
            };

            var validationResult = new ValidationResult
            {
                IsValid = true,
                Warnings = new List<string>()
            };

            var coinbaseResponse = new JsonObject
            {
                ["order_id"] = "coinbase-order-123"
            };

            _mockOrderValidation.Setup(x => x.ValidateOrderAsync(It.IsAny<CreateOrderRequestDto>()))
                .ReturnsAsync(validationResult);

            _mockCoinbaseOrderClient.Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()))
                .ReturnsAsync(coinbaseResponse);

            // Act
            var result = await _controller.CreateOrder(orderRequest);

            // Assert
            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
            
            var okResult = result.Result as OkObjectResult;
            Assert.IsNotNull(okResult);
            
            var orderResponse = okResult.Value as OrderResponse;
            Assert.IsNotNull(orderResponse);
            Assert.AreEqual("LONG", orderResponse.PositionType); // Should preserve the position type
            
            // Verify that order validation was called
            _mockOrderValidation.Verify(x => x.ValidateOrderAsync(It.IsAny<CreateOrderRequestDto>()), Times.Once);
            
            // Verify that Coinbase order creation was called
            _mockCoinbaseOrderClient.Verify(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()), Times.Once);
        }
    }
} 