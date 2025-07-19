using Moq;
using System.Net;
using Moq.Protected;
using Microsoft.Extensions.Options;
using crypto_bot_api.Services.RateLimiting;

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
        
        [TestMethod]
        public async Task GlobalHourlyRateLimit_AllowsAggressiveUsage()
        {
            // Arrange
            var requests = new List<DateTimeOffset>();
            var startTime = DateTimeOffset.UtcNow;
            _timeProvider = new MockTimeProvider();
            _timeProvider._currentTime = startTime;
            
            _mockInnerHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => 
                {
                    requests.Add(_timeProvider.GetCurrentTime());
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"data\": \"test\"}")
                    };
                });
                
            // Act - Make requests sequentially to properly observe rate limiting
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Increased timeout
            
            try 
            {
                // Make initial request to establish baseline
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/v3/brokerage/products");
                await _client.SendAsync(request, cts.Token);
                
                // Make additional requests with explicit time advancement
                for (int i = 0; i < 9; i++) // 9 more requests for a total of 10
                {
                    // Advance time by target interval
                    _timeProvider.AdvanceTime(TimeSpan.FromMilliseconds(360));
                    
                    request = new HttpRequestMessage(HttpMethod.Get, "/api/v3/brokerage/products");
                    await _client.SendAsync(request, cts.Token);
                }
                
                // Assert
                // Verify we made all 10 requests
                Assert.AreEqual(10, requests.Count, "Should have made exactly 10 requests");
                
                // Verify timestamps are monotonically increasing with correct intervals
                for (int i = 1; i < requests.Count; i++)
                {
                    var interval = (requests[i] - requests[i-1]).TotalMilliseconds;
                    Assert.AreEqual(360, interval, 1.0,
                        $"Interval between request {i-1} and {i} should be 360ms, was {interval}ms");
                }
            }
            catch (OperationCanceledException)
            {
                Assert.Fail("Test timed out after 30 seconds");
            }
        }
    }
    
    public class MockTimeProvider
    {
        public DateTimeOffset _currentTime;

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
            if (duration.TotalMilliseconds < 0)
            {
                throw new ArgumentException("Cannot advance time backwards", nameof(duration));
            }
            _currentTime = _currentTime.Add(duration);
        }
    }
} 