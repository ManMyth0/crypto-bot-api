using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using crypto_bot_api.Data;
using crypto_bot_api.Models;
using crypto_bot_api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class ProductInfoServiceTests
    {
        private AppDbContext _dbContext = null!;
        private Mock<HttpMessageHandler> _mockHttpHandler = null!;
        private HttpClient _httpClient = null!;
        private Mock<ILogger<ProductInfoService>> _mockLogger = null!;
        private ProductInfoService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new AppDbContext(options);

            // Setup mock HTTP handler
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object);
            _mockLogger = new Mock<ILogger<ProductInfoService>>();

            _service = new ProductInfoService(_dbContext, _httpClient, _mockLogger.Object);
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
            _httpClient.Dispose();
        }

        private void SetupMockHttpResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                });
        }

        [TestMethod]
        public async Task GetProductInfoAsync_ProductNotFound_RefreshesFromApi()
        {
            // Arrange
            var mockResponse = new
            {
                products = new[]
                {
                    new
                    {
                        product_id = "BTC-USD",
                        base_currency = "BTC",
                        quote_currency = "USD",
                        quote_increment = "0.01",
                        base_increment = "0.00000001",
                        display_name = "BTC/USD",
                        min_market_funds = "10",
                        margin_enabled = false,
                        post_only = false,
                        limit_only = false,
                        cancel_only = false,
                        status = "online",
                        status_message = "",
                        trading_disabled = false,
                        fx_stablecoin = false,
                        max_slippage_percentage = "0.02",
                        auction_mode = false,
                        high_bid_limit_percentage = ""
                    }
                }
            };

            SetupMockHttpResponse(JsonSerializer.Serialize(mockResponse));

            // Act
            var result = await _service.GetProductInfoAsync("BTC-USD");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("BTC-USD", result.ProductId);
            Assert.AreEqual("BTC", result.BaseCurrency);
            Assert.AreEqual("USD", result.QuoteCurrency);
            Assert.AreEqual(0.00000001m, result.BaseIncrement);
            Assert.AreEqual(10m, result.MinMarketFunds);
        }

        [TestMethod]
        public async Task GetProductInfoAsync_ProductExists_ReturnsFromDatabase()
        {
            // Arrange
            var existingProduct = new ProductInfo
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = "ETH-USD",
                BaseCurrency = "ETH",
                QuoteCurrency = "USD",
                BaseIncrement = 0.00001m,
                MinMarketFunds = 5m,
                LastUpdated = DateTime.UtcNow,
                Status = "online",
                StatusMessage = "",
                DisplayName = "ETH/USD",
                HighBidLimitPercentage = ""
            };

            await _dbContext.ProductInfo.AddAsync(existingProduct);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.GetProductInfoAsync("ETH-USD");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(existingProduct.ProductId, result.ProductId);
            Assert.AreEqual(existingProduct.BaseIncrement, result.BaseIncrement);
            
            // Verify no HTTP call was made
            _mockHttpHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            );
        }

        [TestMethod]
        public async Task GetProductInfoAsync_StaleData_RefreshesFromApi()
        {
            // Arrange
            var staleProduct = new ProductInfo
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = "BTC-USD",
                BaseCurrency = "BTC",
                QuoteCurrency = "USD",
                BaseIncrement = 0.00000001m,
                MinMarketFunds = 10m,
                LastUpdated = DateTime.UtcNow.AddDays(-2), // Stale data
                Status = "online",
                StatusMessage = "",
                DisplayName = "BTC/USD",
                HighBidLimitPercentage = ""
            };

            await _dbContext.ProductInfo.AddAsync(staleProduct);
            await _dbContext.SaveChangesAsync();

            var mockResponse = new
            {
                products = new[]
                {
                    new
                    {
                        product_id = "BTC-USD",
                        base_currency = "BTC",
                        quote_currency = "USD",
                        quote_increment = "0.01",
                        base_increment = "0.00000002", // Updated value
                        display_name = "BTC/USD",
                        min_market_funds = "15", // Updated value
                        margin_enabled = false,
                        post_only = false,
                        limit_only = false,
                        cancel_only = false,
                        status = "online",
                        status_message = "",
                        trading_disabled = false,
                        fx_stablecoin = false,
                        max_slippage_percentage = "0.02",
                        auction_mode = false,
                        high_bid_limit_percentage = ""
                    }
                }
            };

            SetupMockHttpResponse(JsonSerializer.Serialize(mockResponse));

            // Act
            var result = await _service.GetProductInfoAsync("BTC-USD");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0.00000002m, result.BaseIncrement); // Verify updated value
            Assert.AreEqual(15m, result.MinMarketFunds); // Verify updated value
            
            // Verify HTTP call was made
            _mockHttpHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            );
        }

        [TestMethod]
        [ExpectedException(typeof(HttpRequestException))]
        public async Task RefreshProductInfoAsync_ApiError_ThrowsException()
        {
            // Arrange
            SetupMockHttpResponse("", HttpStatusCode.InternalServerError);

            // Act
            await _service.RefreshProductInfoAsync();
        }

        [TestMethod]
        public async Task GetProductInfoAsync_ApiErrorWithExistingData_UsesCache()
        {
            // Arrange
            var existingProduct = new ProductInfo
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = "BTC-USD",
                BaseCurrency = "BTC",
                QuoteCurrency = "USD",
                BaseIncrement = 0.00000001m,
                MinMarketFunds = 10m,
                LastUpdated = DateTime.UtcNow.AddDays(-2), // Stale data
                Status = "online",
                StatusMessage = "",
                DisplayName = "BTC/USD",
                HighBidLimitPercentage = ""
            };

            await _dbContext.ProductInfo.AddAsync(existingProduct);
            await _dbContext.SaveChangesAsync();

            SetupMockHttpResponse("", HttpStatusCode.InternalServerError);

            // Act
            var result = await _service.GetProductInfoAsync("BTC-USD");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(existingProduct.ProductId, result.ProductId);
            Assert.AreEqual(existingProduct.BaseIncrement, result.BaseIncrement);
            
            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to refresh product info")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
                Times.Once
            );
        }
    }
} 