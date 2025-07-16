using System;
using System.Threading.Tasks;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models;
using crypto_bot_api.Models.DTOs.Orders;
using crypto_bot_api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class OrderValidationServiceTests
    {
        private Mock<IProductInfoService> _mockProductInfoService = null!;
        private Mock<ILogger<OrderValidationService>> _mockLogger = null!;
        private OrderValidationService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockProductInfoService = new Mock<IProductInfoService>();
            _mockLogger = new Mock<ILogger<OrderValidationService>>();
            _service = new OrderValidationService(_mockProductInfoService.Object, _mockLogger.Object);
        }

        private static CreateOrderRequestDto CreateLimitOrderRequest(string productId, string baseSize, string quoteSize, string limitPrice)
        {
            return new CreateOrderRequestDto
            {
                ProductId = productId,
                Side = "BUY",
                PositionType = "LONG",
                ClientOrderId = Guid.NewGuid().ToString(),
                OrderConfiguration = new OrderConfigurationDto
                {
                    LimitLimitGtc = new LimitLimitGtcDto
                    {
                        BaseSize = baseSize,
                        QuoteSize = quoteSize,
                        LimitPrice = limitPrice
                    }
                }
            };
        }

        private static CreateOrderRequestDto CreateMarketOrderRequest(string productId, string baseSize, string quoteSize)
        {
            return new CreateOrderRequestDto
            {
                ProductId = productId,
                Side = "BUY",
                PositionType = "LONG",
                ClientOrderId = Guid.NewGuid().ToString(),
                OrderConfiguration = new OrderConfigurationDto
                {
                    MarketMarket = new MarketMarketConfig
                    {
                        BaseSize = baseSize,
                        QuoteSize = quoteSize
                    }
                }
            };
        }

        [TestMethod]
        public async Task ValidateOrderAsync_ValidOrder_Succeeds()
        {
            // Arrange
            var orderRequest = CreateLimitOrderRequest(
                productId: "BTC-USD",
                baseSize: "0.001",
                quoteSize: "50.00",
                limitPrice: "50000.00"
            );

            _mockProductInfoService.Setup(x => x.GetProductInfoAsync("BTC-USD"))
                .ReturnsAsync(new ProductInfo
                {
                    ProductId = "BTC-USD",
                    BaseIncrement = 0.00001m,
                    MinMarketFunds = 10m,
                    Status = "online",
                    StatusMessage = "",
                    DisplayName = "BTC/USD",
                    HighBidLimitPercentage = "",
                    TradingDisabled = false
                });

            // Act
            await _service.ValidateOrderAsync(orderRequest);
            // No exception means validation passed
        }

        [TestMethod]
        [ExpectedException(typeof(CoinbaseApiException))]
        public async Task ValidateOrderAsync_ProductNotFound_ThrowsException()
        {
            // Arrange
            var orderRequest = CreateLimitOrderRequest(
                productId: "INVALID-PAIR",
                baseSize: "0.001",
                quoteSize: "50.00",
                limitPrice: "50000.00"
            );

            _mockProductInfoService.Setup(x => x.GetProductInfoAsync("INVALID-PAIR"))
                .ReturnsAsync((ProductInfo?)null);

            // Act
            await _service.ValidateOrderAsync(orderRequest);
        }

        [TestMethod]
        public async Task ValidateOrderAsync_TradingDisabled_ThrowsException()
        {
            // Arrange
            var orderRequest = CreateLimitOrderRequest(
                productId: "BTC-USD",
                baseSize: "0.001",
                quoteSize: "50.00",
                limitPrice: "50000.00"
            );

            _mockProductInfoService.Setup(x => x.GetProductInfoAsync("BTC-USD"))
                .ReturnsAsync(new ProductInfo
                {
                    ProductId = "BTC-USD",
                    TradingDisabled = true,
                    Status = "offline",
                    StatusMessage = "",
                    DisplayName = "BTC/USD",
                    HighBidLimitPercentage = ""
                });

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<CoinbaseApiException>(
                () => _service.ValidateOrderAsync(orderRequest)
            );
            StringAssert.Contains(exception.Message, "Trading is disabled");
        }

        [TestMethod]
        public async Task ValidateOrderAsync_BelowMinimumFunds_ThrowsException()
        {
            // Arrange
            var orderRequest = CreateLimitOrderRequest(
                productId: "BTC-USD",
                baseSize: "0.001",
                quoteSize: "5.00",
                limitPrice: "5000.00"
            );

            _mockProductInfoService.Setup(x => x.GetProductInfoAsync("BTC-USD"))
                .ReturnsAsync(new ProductInfo
                {
                    ProductId = "BTC-USD",
                    MinMarketFunds = 10m,
                    Status = "online",
                    StatusMessage = "",
                    DisplayName = "BTC/USD",
                    HighBidLimitPercentage = "",
                    TradingDisabled = false,
                    BaseIncrement = 0.00001m
                });

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<CoinbaseApiException>(
                () => _service.ValidateOrderAsync(orderRequest)
            );
            StringAssert.Contains(exception.Message, "below minimum");
        }

        [TestMethod]
        public async Task ValidateOrderAsync_InvalidBaseIncrement_ThrowsException()
        {
            // Arrange
            var orderRequest = CreateLimitOrderRequest(
                productId: "BTC-USD",
                baseSize: "0.0001234",
                quoteSize: "50.00",
                limitPrice: "50000.00"
            );

            _mockProductInfoService.Setup(x => x.GetProductInfoAsync("BTC-USD"))
                .ReturnsAsync(new ProductInfo
                {
                    ProductId = "BTC-USD",
                    BaseIncrement = 0.00001m,
                    MinMarketFunds = 10m,
                    Status = "online",
                    StatusMessage = "",
                    DisplayName = "BTC/USD",
                    HighBidLimitPercentage = "",
                    TradingDisabled = false
                });

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<CoinbaseApiException>(
                () => _service.ValidateOrderAsync(orderRequest)
            );
            StringAssert.Contains(exception.Message, "Must be an increment of");
        }

        [TestMethod]
        public async Task ValidateOrderAsync_ProductDelisted_ThrowsException()
        {
            // Arrange
            var orderRequest = CreateLimitOrderRequest(
                productId: "BTC-USD",
                baseSize: "0.001",
                quoteSize: "50.00",
                limitPrice: "50000.00"
            );

            _mockProductInfoService.Setup(x => x.GetProductInfoAsync("BTC-USD"))
                .ReturnsAsync(new ProductInfo
                {
                    ProductId = "BTC-USD",
                    Status = "delisted",
                    StatusMessage = "",
                    DisplayName = "BTC/USD",
                    HighBidLimitPercentage = "",
                    TradingDisabled = false
                });

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<CoinbaseApiException>(
                () => _service.ValidateOrderAsync(orderRequest)
            );
            StringAssert.Contains(exception.Message.ToLower(), "delisted");
        }

        [TestMethod]
        public async Task ValidateOrderAsync_LimitOnlyProduct_NonLimitOrder_ThrowsException()
        {
            // Arrange
            var orderRequest = CreateMarketOrderRequest(
                productId: "BTC-USD",
                baseSize: "0.001",
                quoteSize: "50.00"
            );

            _mockProductInfoService.Setup(x => x.GetProductInfoAsync("BTC-USD"))
                .ReturnsAsync(new ProductInfo
                {
                    ProductId = "BTC-USD",
                    LimitOnly = true,
                    Status = "online",
                    StatusMessage = "",
                    DisplayName = "BTC/USD",
                    HighBidLimitPercentage = "",
                    TradingDisabled = false
                });

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<CoinbaseApiException>(
                () => _service.ValidateOrderAsync(orderRequest)
            );
            StringAssert.Contains(exception.Message.ToLower(), "limit orders only");
        }
    }
} 