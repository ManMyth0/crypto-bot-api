using Moq;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using crypto_bot_api.Services.RateLimiting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq.Protected;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class CoinbaseRateLimitHandlerTests
    {
        private Mock<IOptions<RateLimitOptions>> _mockOptions = null!;
        private Mock<ILogger<CoinbaseRateLimitHandler>> _mockLogger = null!;
        private Mock<HttpMessageHandler> _mockInnerHandler = null!;
        private RateLimitOptions _options = null!;
        private HttpMessageInvoker _invoker = null!;
        
        [TestInitialize]
        public void TestInitialize()
        {
            // Set up options with new values
            _options = new RateLimitOptions
            {
                PublicEndpointRateLimit = 9,
                PrivateEndpointRateLimit = 30,
                LastRemainingPrivateRequests = 30
            };
            
            _mockOptions = new Mock<IOptions<RateLimitOptions>>();
            _mockOptions.Setup(o => o.Value).Returns(_options);
            
            _mockLogger = new Mock<ILogger<CoinbaseRateLimitHandler>>();
            
            // Set up the mock inner handler
            _mockInnerHandler = new Mock<HttpMessageHandler>();
            
            // Create our handler with the mock dependencies
            var handler = new CoinbaseRateLimitHandler(_mockOptions.Object, _mockLogger.Object)
            {
                InnerHandler = _mockInnerHandler.Object
            };
            
            // Set up the invoker with our handler
            _invoker = new HttpMessageInvoker(handler);
        }
        
        [TestMethod]
        public async Task PublicEndpoint_EnforcesRateLimit()
        {
            // Arrange
            SetupMockResponse(HttpStatusCode.OK);
            var maxRequests = _options.PublicEndpointRateLimit;
            
            // Act & Assert
            // Should allow exactly the rate limit of requests without delay
            for (int i = 0; i < maxRequests; i++)
            {
                var startTime = DateTime.UtcNow;
                await _invoker.SendAsync(CreatePublicRequest(), CancellationToken.None);
                var elapsed = DateTime.UtcNow - startTime;
                
                // Should process without significant delay (< 50ms)
                Assert.IsTrue(elapsed.TotalMilliseconds < 50, 
                    $"Request {i+1} took {elapsed.TotalMilliseconds}ms which is longer than expected");
            }
            
            // Verify no logs were produced during normal operation
            VerifyNoLogsProduced();
            
            // The next request should be delayed since we've hit our self-imposed limit
            var finalStartTime = DateTime.UtcNow;
            await _invoker.SendAsync(CreatePublicRequest(), CancellationToken.None);
            var finalElapsed = DateTime.UtcNow - finalStartTime;
            
            // Should have some delay due to rate limiting (at least 80ms)
            Assert.IsTrue(finalElapsed.TotalMilliseconds >= 80,
                $"Expected delay from rate limiting, but request took only {finalElapsed.TotalMilliseconds}ms");
            
            // Verify count of requests in queue
            Assert.AreEqual(maxRequests + 1, _options.RecentPublicRequests.Count);
            
            // Still verify no logs were produced (even when self-throttling, but not exceeding external limits)
            VerifyNoLogsProduced();
        }
        
        [TestMethod]
        public async Task PrivateEndpoint_ProactivelyThrottlesWhenNearingLimit()
        {
            // Arrange
            SetupMockResponse(HttpStatusCode.OK, remainingRequests: 30);
            
            // Act & Assert
            // First request with high remaining count - should be fast
            var startTime = DateTime.UtcNow;
            await _invoker.SendAsync(CreatePrivateRequest(), CancellationToken.None);
            var elapsed = DateTime.UtcNow - startTime;
            
            // Should be quick (< 50ms)
            Assert.IsTrue(elapsed.TotalMilliseconds < 50);
            
            // Verify no logs for normal operation
            VerifyNoLogsProduced();
            
            // Now set remaining requests to 1 to trigger proactive throttling
            // This simulates when we're about to hit the limit but haven't yet
            _options.LastRemainingPrivateRequests = 1;
            
            // Second request should be proactively delayed to avoid hitting limits
            startTime = DateTime.UtcNow;
            await _invoker.SendAsync(CreatePrivateRequest(), CancellationToken.None);
            elapsed = DateTime.UtcNow - startTime;
            
            // Should have delay (at least 80ms due to the 100ms proactive delay applied)
            Assert.IsTrue(elapsed.TotalMilliseconds >= 80,
                $"Expected proactive delay for low remaining requests, but only took {elapsed.TotalMilliseconds}ms");
            
            // Verify still no logs even when proactively throttling (but not exceeding)
            VerifyNoLogsProduced();
        }
        
        [TestMethod]
        public async Task RateLimitExceeded_ImplementsLinearBackoff()
        {
            // Arrange
            // First response is rate limited, second succeeds
            SetupRateLimitedThenSuccessResponse();
            
            // Act
            var startTime = DateTime.UtcNow;
            await _invoker.SendAsync(CreatePrivateRequest(), CancellationToken.None);
            var elapsed = DateTime.UtcNow - startTime;
            
            // Assert
            // Should have delayed with linear backoff, so we expect at least 1000ms delay
            // from first retry in the linear backoff pattern
            Assert.IsTrue(elapsed.TotalMilliseconds >= 1000,
                $"Expected linear backoff delay, but only took {elapsed.TotalMilliseconds}ms");
            
            // Verify that we get logs ONLY when actually hitting a rate limit
            VerifyRateLimitLogsProduced();
            
            // We have more detailed verification in the mock setup - this verifies the sequence of calls
            _mockInnerHandler.Protected().Verify(
                "SendAsync",
                Times.Exactly(2),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }
        
        #region Helper Methods
        
        private HttpRequestMessage CreatePublicRequest()
        {
            // Public requests have no Authorization header
            return new HttpRequestMessage(HttpMethod.Get, "https://api.coinbase.com/api/v3/brokerage/products");
        }
        
        private HttpRequestMessage CreatePrivateRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.coinbase.com/api/v3/brokerage/orders");
            request.Headers.Add("Authorization", "Bearer test-token");
            return request;
        }
        
        private void SetupMockResponse(HttpStatusCode statusCode, int remainingRequests = 30)
        {
            _mockInnerHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => {
                    var response = new HttpResponseMessage(statusCode);
                    response.Headers.Add("x-ratelimit-limit", "30");
                    response.Headers.Add("x-ratelimit-remaining", remainingRequests.ToString());
                    response.Headers.Add("x-ratelimit-reset", "1");
                    return response;
                });
        }
        
        private void SetupRateLimitedThenSuccessResponse()
        {
            var callCount = 0;
            
            _mockInnerHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => {
                    callCount++;
                    
                    if (callCount == 1)
                    {
                        // First call returns TooManyRequests
                        return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                        {
                            Content = new StringContent("{\"error\":\"rate_limit_exceeded\"}")
                        };
                    }
                    else
                    {
                        // Subsequent calls return OK
                        var response = new HttpResponseMessage(HttpStatusCode.OK);
                        response.Headers.Add("x-ratelimit-limit", "30");
                        response.Headers.Add("x-ratelimit-remaining", "29");
                        response.Headers.Add("x-ratelimit-reset", "1");
                        return response;
                    }
                });
        }
        
        private void VerifyNoLogsProduced()
        {
            // Verify that no logs at any level were produced
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }
        
        private void VerifyRateLimitLogsProduced()
        {
            // Verify that warning logs were produced (for rate limit exceeded)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("rate limit exceeded")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        
        #endregion
    }
} 