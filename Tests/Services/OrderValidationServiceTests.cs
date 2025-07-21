using Moq;
using crypto_bot_api.Models;
using crypto_bot_api.Services;
using crypto_bot_api.Models.DTOs.Orders;

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
                    MarketMarketIoc = new MarketMarketIocConfig
                    {
                        BaseSize = baseSize,
                        QuoteSize = quoteSize,
                        RfqDisabled = true
                    }
                }
            };
        }

        [TestMethod]
        public async Task ValidateOrderAsync_ValidOrder_NoWarnings()
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
            var result = await _service.ValidateOrderAsync(orderRequest);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Warnings.Count);
            Assert.AreEqual("BTC-USD", result.ProductId);
            Assert.AreEqual("online", result.Status);
            Assert.IsFalse(result.TradingDisabled);
        }

        [TestMethod]
        public async Task ValidateOrderAsync_ProductNotFound_ReturnsWarning()
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
            var result = await _service.ValidateOrderAsync(orderRequest);

            // Assert
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("not found")));
        }

        [TestMethod]
        public async Task ValidateOrderAsync_TradingDisabled_ReturnsWarning()
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
                    HighBidLimitPercentage = "",
                    BaseIncrement = 0.00001m,
                    MinMarketFunds = 10m
                });

            // Act
            var result = await _service.ValidateOrderAsync(orderRequest);

            // Assert
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("disabled")));
            Assert.IsTrue(result.TradingDisabled);
        }

        [TestMethod]
        public async Task ValidateOrderAsync_BelowMinimumFunds_ReturnsWarning()
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

            // Act
            var result = await _service.ValidateOrderAsync(orderRequest);

            // Assert
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("below minimum")));
        }

        [TestMethod]
        public async Task ValidateOrderAsync_InvalidBaseIncrement_ReturnsWarning()
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

            // Act
            var result = await _service.ValidateOrderAsync(orderRequest);

            // Assert
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("increment")));
        }

        [TestMethod]
        public async Task ValidateOrderAsync_ProductDelisted_ReturnsWarning()
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
                    TradingDisabled = false,
                    BaseIncrement = 0.00001m,
                    MinMarketFunds = 10m
                });

            // Act
            var result = await _service.ValidateOrderAsync(orderRequest);

            // Assert
            Assert.IsTrue(result.Warnings.Any(w => w.ToLower().Contains("delisted")));
        }

        [TestMethod]
        public async Task ValidateOrderAsync_LimitOnlyProduct_NonLimitOrder_ReturnsWarning()
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
                    TradingDisabled = false,
                    BaseIncrement = 0.00001m,
                    MinMarketFunds = 10m
                });

            // Act
            var result = await _service.ValidateOrderAsync(orderRequest);

            // Assert
            Assert.IsTrue(result.Warnings.Any(w => w.ToLower().Contains("limit orders only")));
        }
    }
} 