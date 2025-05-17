using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using crypto_bot_api.CustomExceptions;

namespace crypto_bot_api.Services.RateLimiting
{
    public class CoinbaseRateLimitHandler : DelegatingHandler
    {
        private readonly RateLimitOptions _options;
        private readonly ILogger<CoinbaseRateLimitHandler> _logger;
        private readonly Random _random = new();
        
        // Maximum retries for rate limit errors
        private const int MaxRetryCount = 3;
        // Base delay for exponential backoff (in milliseconds)
        private const int BaseRetryDelayMs = 1000;
        // Small delay when we're at the last remaining request
        private const int LowRemainingDelayMs = 100;
        
        public CoinbaseRateLimitHandler(IOptions<RateLimitOptions> options, ILogger<CoinbaseRateLimitHandler> logger)
        {
            _options = options.Value;
            _logger = logger;
        }
        
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            // Determine if this is a public or private endpoint
            bool isPublicEndpoint = request.Headers.Authorization == null;
            
            // For public endpoints, enforce our own rate limit
            if (isPublicEndpoint)
            {
                await EnforcePublicEndpointRateLimit(cancellationToken);
            }
            else // For private endpoints, check if we need to throttle based on previous remaining count
            {
                if (_options.LastRemainingPrivateRequests <= 1)
                {
                    // Apply a small delay to avoid hitting the rate limit - silent operation
                    await Task.Delay(LowRemainingDelayMs, cancellationToken);
                }
            }
            
            // Send the request with retry logic for rate limit errors
            HttpResponseMessage? response = null;
            int retryCount = 0;
            
            while (retryCount <= MaxRetryCount)
            {
                try
                {
                    response = await base.SendAsync(request, cancellationToken);
                    
                    // If not a rate limit error, break the loop
                    if (response.StatusCode != HttpStatusCode.TooManyRequests)
                    {
                        break;
                    }
                    
                    // Only log when we actually hit a rate limit
                    LogRateLimitExceeded(response, request.RequestUri!, isPublicEndpoint);
                    
                    // Calculate backoff delay with linear increase and jitter
                    var backoffDelayMs = BaseRetryDelayMs * (retryCount + 1);
                    var jitter = _random.Next(0, 100); // Add 0-100ms of random jitter
                    var retryDelay = TimeSpan.FromMilliseconds(backoffDelayMs + jitter);
                    
                    await Task.Delay(retryDelay, cancellationToken);
                    retryCount++;
                    
                    // Create a new request for retry (can't reuse the original)
                    var retryRequest = CopyRequest(request);
                    request = retryRequest;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending request to {Url}", request.RequestUri);
                    throw;
                }
            }
            
            // If we exhausted all retries and still got rate limited
            if (response?.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new CoinbaseApiException($"Rate limit exceeded after {MaxRetryCount} retries. Response: {responseContent}");
            }
            
            // For private endpoints, silently update the remaining request count without logging
            if (!isPublicEndpoint && response != null)
            {
                UpdatePrivateEndpointRateLimitState(response);
            }
            
            return response!;
        }
        
        private async Task EnforcePublicEndpointRateLimit(CancellationToken cancellationToken)
        {
            // Clean up timestamps older than 1 second
            while (_options.RecentPublicRequests.TryPeek(out DateTime oldestTime) && 
                  (DateTime.UtcNow - oldestTime).TotalSeconds > 1)
            {
                _options.RecentPublicRequests.TryDequeue(out _);
            }
            
            // If we've reached the limit of public requests per second, wait silently
            if (_options.RecentPublicRequests.Count >= _options.PublicEndpointRateLimit)
            {
                // Small delay to spread requests - silent operation
                await Task.Delay(100, cancellationToken);
            }
            
            // Record this request timestamp
            _options.RecentPublicRequests.Enqueue(DateTime.UtcNow);
        }
        
        private void UpdatePrivateEndpointRateLimitState(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("x-ratelimit-remaining", out var remainingValues) &&
                remainingValues.FirstOrDefault() is string remainingValue &&
                int.TryParse(remainingValue, out int remaining))
            {
                _options.LastRemainingPrivateRequests = remaining;
            }
        }
        
        private HttpRequestMessage CopyRequest(HttpRequestMessage request)
        {
            var newRequest = new HttpRequestMessage(request.Method, request.RequestUri);
            
            // Copy headers
            foreach (var header in request.Headers)
            {
                newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            // Copy content if present
            if (request.Content != null)
            {
                newRequest.Content = request.Content;
            }
            
            return newRequest;
        }
        
        private void LogRateLimitExceeded(HttpResponseMessage response, Uri requestUri, bool isPublicEndpoint)
        {
            string endpointType = isPublicEndpoint ? "Public" : "Private";
            string limitValue = GetHeaderValue(response, "x-ratelimit-limit");
            string remainingValue = GetHeaderValue(response, "x-ratelimit-remaining");
            string resetValue = GetHeaderValue(response, "x-ratelimit-reset");
            
            _logger.LogWarning("{EndpointType} endpoint rate limit exceeded for {Url}. Limit={Limit}, Remaining={Remaining}, Reset={Reset}",
                endpointType, requestUri, limitValue, remainingValue, resetValue);
        }
        
        private string GetHeaderValue(HttpResponseMessage response, string headerName)
        {
            if (response.Headers.TryGetValues(headerName, out var values))
            {
                return values.FirstOrDefault() ?? string.Empty;
            }
            return string.Empty;
        }
    }
} 