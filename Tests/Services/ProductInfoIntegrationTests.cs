using Moq;
using System;
using System.Net;
using Moq.Protected;
using System.Net.Http;
using System.Threading.Tasks;
using crypto_bot_api.Data;
using crypto_bot_api.Models;
using crypto_bot_api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class ProductInfoIntegrationTests
    {
        private AppDbContext? _context;
        private IConfiguration? _configuration;
        private Mock<HttpMessageHandler> _mockHttpHandler = null!;
        private HttpClient _httpClient = null!;
        private Mock<ILogger<ProductInfoService>> _mockLogger = null!;
        private ProductInfoService _service = null!;

        private const string MOCK_PRODUCT_RESPONSE = @"[
            {
                ""product_id"": ""BTC-USD"",
                ""base_currency"": ""BTC"",
                ""quote_currency"": ""USD"",
                ""quote_increment"": ""0.01"",
                ""base_increment"": ""0.00001"",
                ""display_name"": ""BTC/USD"",
                ""min_market_funds"": ""10.00"",
                ""margin_enabled"": false,
                ""post_only"": false,
                ""limit_only"": false,
                ""cancel_only"": false,
                ""status"": ""online"",
                ""status_message"": """",
                ""trading_disabled"": false,
                ""fx_stablecoin"": false,
                ""max_slippage_percentage"": ""0.03"",
                ""auction_mode"": false,
                ""high_bid_limit_percentage"": """"
            },
            {
                ""product_id"": ""ETH-USD"",
                ""base_currency"": ""ETH"",
                ""quote_currency"": ""USD"",
                ""quote_increment"": ""0.01"",
                ""base_increment"": ""0.0001"",
                ""display_name"": ""ETH/USD"",
                ""min_market_funds"": ""10.00"",
                ""margin_enabled"": false,
                ""post_only"": false,
                ""limit_only"": true,
                ""cancel_only"": false,
                ""status"": ""online"",
                ""status_message"": """",
                ""trading_disabled"": false,
                ""fx_stablecoin"": false,
                ""max_slippage_percentage"": ""0.03"",
                ""auction_mode"": false,
                ""high_bid_limit_percentage"": """"
            }
        ]";

        [TestInitialize]
        public void Setup()
        {
            // Build configuration to access user secrets
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets<ProductInfoIntegrationTests>();
            
            _configuration = builder.Build();

            // Get connection string from user secrets
            var connectionString = _configuration["PostgresLocalDatabaseConnection"];
            
            // Create DbContext options
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .EnableSensitiveDataLogging() // Helpful for debugging
                .Options;

            _context = new AppDbContext(options);

            // Setup mock HTTP handler
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object);
            _mockLogger = new Mock<ILogger<ProductInfoService>>();

            // Setup mock response for product info
            _mockHttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri != null &&
                        req.RequestUri.ToString().Contains("products")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(MOCK_PRODUCT_RESPONSE)
                });

            _service = new ProductInfoService(_context, _httpClient, _mockLogger.Object);
        }

        [TestMethod]
        public async Task InitializeAsync_PopulatesDatabase_WithProductInfo()
        {
            try
            {
                // Act
                await _service.InitializeAsync();

                // Assert
                var btcProduct = await _context!.ProductInfo
                    .FirstOrDefaultAsync(p => p.ProductId == "BTC-USD");
                
                Assert.IsNotNull(btcProduct, "BTC-USD product should be in database");
                Assert.AreEqual("BTC", btcProduct.BaseCurrency);
                Assert.AreEqual("USD", btcProduct.QuoteCurrency);
                Assert.AreEqual(0.00001m, btcProduct.BaseIncrement);
                Assert.AreEqual(10.00m, btcProduct.MinMarketFunds);
                Assert.AreEqual("online", btcProduct.Status);
                Assert.IsFalse(btcProduct.TradingDisabled);
                Assert.IsFalse(btcProduct.LimitOnly);

                var ethProduct = await _context.ProductInfo
                    .FirstOrDefaultAsync(p => p.ProductId == "ETH-USD");
                
                Assert.IsNotNull(ethProduct, "ETH-USD product should be in database");
                Assert.AreEqual("ETH", ethProduct.BaseCurrency);
                Assert.AreEqual("USD", ethProduct.QuoteCurrency);
                Assert.AreEqual(0.0001m, ethProduct.BaseIncrement);
                Assert.AreEqual(10.00m, ethProduct.MinMarketFunds);
                Assert.AreEqual("online", ethProduct.Status);
                Assert.IsFalse(ethProduct.TradingDisabled);
                Assert.IsTrue(ethProduct.LimitOnly);
            }
            finally
            {
                // Cleanup
                var productsToDelete = await _context!.ProductInfo.ToListAsync();
                _context.ProductInfo.RemoveRange(productsToDelete);
                await _context.SaveChangesAsync();
            }
        }

        [TestMethod]
        public async Task GetProductInfoAsync_ReturnsCachedData_WhenAvailable()
        {
            try
            {
                // Arrange
                await _service.InitializeAsync();

                // Act
                var cachedProduct = await _service.GetProductInfoAsync("BTC-USD");

                // Assert
                Assert.IsNotNull(cachedProduct);
                Assert.AreEqual("BTC-USD", cachedProduct.ProductId);
                Assert.AreEqual("BTC", cachedProduct.BaseCurrency);
                Assert.AreEqual(0.00001m, cachedProduct.BaseIncrement);

                // Verify we didn't make another HTTP call
                _mockHttpHandler.Protected()
                    .Verify<Task<HttpResponseMessage>>(
                        "SendAsync",
                        Times.Exactly(1), // Only from initialization
                        ItExpr.Is<HttpRequestMessage>(req =>
                            req.Method == HttpMethod.Get &&
                            req.RequestUri != null &&
                            req.RequestUri.ToString().Contains("products")),
                        ItExpr.IsAny<CancellationToken>());
            }
            finally
            {
                // Cleanup
                var productsToDelete = await _context!.ProductInfo.ToListAsync();
                _context.ProductInfo.RemoveRange(productsToDelete);
                await _context.SaveChangesAsync();
            }
        }

        [TestMethod]
        public async Task GetProductInfoAsync_RefreshesData_WhenStale()
        {
            try
            {
                // Arrange
                await _service.InitializeAsync();

                // Modify last updated time to make data stale
                var existingProducts = await _context!.ProductInfo.ToListAsync();
                foreach (var existingProduct in existingProducts)
                {
                    existingProduct.LastUpdated = DateTime.UtcNow.AddDays(-2); // Make data 2 days old
                }
                await _context.SaveChangesAsync();

                // Act
                var refreshedProduct = await _service.GetProductInfoAsync("BTC-USD");

                // Assert
                Assert.IsNotNull(refreshedProduct);
                Assert.AreEqual("BTC-USD", refreshedProduct.ProductId);
                Assert.IsTrue((DateTime.UtcNow - refreshedProduct.LastUpdated).TotalHours < 1, 
                    "Product info should have been refreshed");

                // Verify we made another HTTP call
                _mockHttpHandler.Protected()
                    .Verify<Task<HttpResponseMessage>>(
                        "SendAsync",
                        Times.Exactly(2), // Once for init, once for refresh
                        ItExpr.Is<HttpRequestMessage>(req =>
                            req.Method == HttpMethod.Get &&
                            req.RequestUri != null &&
                            req.RequestUri.ToString().Contains("products")),
                        ItExpr.IsAny<CancellationToken>());
            }
            finally
            {
                // Cleanup
                var productsToDelete = await _context!.ProductInfo.ToListAsync();
                _context.ProductInfo.RemoveRange(productsToDelete);
                await _context.SaveChangesAsync();
            }
        }

        [TestMethod]
        public async Task GetProductInfoAsync_HandlesApiFailure_GracefullyWithCachedData()
        {
            try
            {
                // Arrange
                await _service.InitializeAsync();

                // Make data stale
                var existingProducts = await _context!.ProductInfo.ToListAsync();
                foreach (var existingProduct in existingProducts)
                {
                    existingProduct.LastUpdated = DateTime.UtcNow.AddDays(-2);
                }
                await _context.SaveChangesAsync();

                // Setup API to fail
                _mockHttpHandler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.Is<HttpRequestMessage>(req =>
                            req.Method == HttpMethod.Get &&
                            req.RequestUri != null &&
                            req.RequestUri.ToString().Contains("products")),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        Content = new StringContent("Server Error")
                    });

                // Act
                var fallbackProduct = await _service.GetProductInfoAsync("BTC-USD");

                // Assert
                Assert.IsNotNull(fallbackProduct, "Should return cached data even if refresh fails");
                Assert.AreEqual("BTC-USD", fallbackProduct.ProductId);
                Assert.IsTrue((DateTime.UtcNow - fallbackProduct.LastUpdated).TotalDays >= 1,
                    "LastUpdated should not be updated due to failed refresh");

                // Verify warning was logged
                _mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to refresh product info")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);
            }
            finally
            {
                // Cleanup
                var productsToDelete = await _context!.ProductInfo.ToListAsync();
                _context.ProductInfo.RemoveRange(productsToDelete);
                await _context.SaveChangesAsync();
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_context != null)
            {
                _context.Dispose();
                _context = null;
            }
            _httpClient.Dispose();
        }
    }
} 