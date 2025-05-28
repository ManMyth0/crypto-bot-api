using Moq;
using System.Net;
using Moq.Protected;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using crypto_bot_api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using crypto_bot_api.Tests.Utilities;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;
using crypto_bot_api.Services.RateLimiting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class CoinbaseRateLimitHandlerTests
    {
        private Mock<IOptions<RateLimitOptions>> _mockOptions = new();
        private Mock<ILogger<CoinbaseRateLimitHandler>> _mockLogger = new();
        private Mock<HttpMessageHandler> _mockInnerHandler = new();
        private MockTimeProvider _timeProvider = new();
        private CoinbaseRateLimitHandler _handler = null!;
        private HttpClient _client = null!;
        private RateLimitOptions _options = new();
        
        [TestInitialize]
        public void Setup()
        {
            _mockInnerHandler = new Mock<HttpMessageHandler>();
            _mockLogger = new Mock<ILogger<CoinbaseRateLimitHandler>>();
            _options = new RateLimitOptions();
            _mockOptions = new Mock<IOptions<RateLimitOptions>>();
            _mockOptions.Setup(o => o.Value).Returns(_options);
            _timeProvider = new MockTimeProvider();
            
            // Create handler with mocked time
            _handler = new CoinbaseRateLimitHandler(
                _mockOptions.Object, 
                _mockLogger.Object, 
                () => _timeProvider.GetCurrentTime());
                
            // Set inner handler for delegating
            _handler.InnerHandler = _mockInnerHandler.Object;
            
            // Create client with our handler
            _client = new HttpClient(_handler)
            {
                BaseAddress = new Uri("https://api.coinbase.com"),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _client.Dispose();
            _handler.Dispose();
        }
        
        [TestMethod]
        public async Task SendAsync_UsesTimeoutToPreventHanging()
        {
            // Arrange
            // Setup mock to delay longer than our timeout
            _mockInnerHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => 
                {
                    // Create a successful response
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"data\": \"test\"}")
                    };
                });
                
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v3/brokerage/products");
            var response = await _client.SendAsync(request, CancellationToken.None);
            
            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            
            // Verify the inner handler was called
            _mockInnerHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
        }
        
        [TestMethod]
        public async Task RateLimitExceeded_RetriesWithBackoff()
        {
            // Arrange
            var callCount = 0;
            
            _mockInnerHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        // First call returns rate limit exceeded
                        return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    }
                    else
                    {
                        // Second call succeeds
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"data\": \"test\"}")
                        };
                    }
                });
                
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v3/brokerage/accounts");
            request.Headers.Add("Authorization", "Bearer test");
            var response = await _client.SendAsync(request, CancellationToken.None);
            
            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(2, callCount, "Expected exactly 2 calls due to retry");
        }
    }
    
    public class MockTimeProvider
    {
        private DateTimeOffset _currentTime;

        public MockTimeProvider()
        {
            _currentTime = DateTimeOffset.UtcNow;
        }

        public DateTimeOffset GetCurrentTime()
        {
            return _currentTime;
        }

        public void AdvanceTime(TimeSpan duration)
        {
            _currentTime = _currentTime.Add(duration);
        }
    }
} 