using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using crypto_bot_api.CustomExceptions;

namespace crypto_bot_api.Services.RateLimiting
{
    public class CoinbaseRateLimitHandler : DelegatingHandler
    {
        private readonly IOptions<RateLimitOptions> _options;
        private readonly ILogger<CoinbaseRateLimitHandler> _logger;
        private readonly Func<DateTimeOffset> _getCurrentTime;
        private readonly Random _random = new();
        
        // Maximum retries for rate limit errors
        private const int MaxRetryCount = 3;
        // Base delay for exponential backoff (in milliseconds)
        private const int BaseRetryDelayMs = 1000;
        // Small delay when we're at the last remaining request
        private const int LowRemainingDelayMs = 100;
        // Target interval between requests to stay under hourly limit (in milliseconds)
        // 3600000ms (1 hour) / 9900 requests = ~364ms between requests on average
        private const int HourlyTargetIntervalMs = 364; 
        // Pattern to extract the first rate limit value
        private static readonly Regex RateLimitPattern = new Regex(@"^(\d+)");
        // Pattern to identify order management endpoints
        private static readonly Regex OrderManagementPattern = new Regex(@"\/orders\/|\/orders$");

        public CoinbaseRateLimitHandler(
            IOptions<RateLimitOptions> options, 
            ILogger<CoinbaseRateLimitHandler> logger,
            Func<DateTimeOffset>? getCurrentTime = null)
        {
            _options = options;
            _logger = logger;
            _getCurrentTime = getCurrentTime ?? (() => DateTimeOffset.UtcNow);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            var now = _getCurrentTime();
            bool isPublicEndpoint = request.Headers.Authorization == null;
            bool isOrderManagementEndpoint = request.RequestUri != null && 
                OrderManagementPattern.IsMatch(request.RequestUri.AbsolutePath);
            
            try
            {
                // Check global hourly limit first (most restrictive)
                await EnforceGlobalHourlyRateLimit(now, cancellationToken);
                
                // Then check per-second limits
                if (isPublicEndpoint)
                {
                    await EnforcePublicEndpointRateLimit(cancellationToken);
                }
                else
                {
                    await EnforcePrivateEndpointRateLimit(isOrderManagementEndpoint, cancellationToken);
                }
                
                // Send the request with retry logic for rate limit errors
                HttpResponseMessage? response = null;
                int retryCount = 0;
                
                while (retryCount <= MaxRetryCount)
                {
                    try
                    {
                        // Record this request for the global hourly limit
                        _options.Value.HourlyRequestsTimestamps.Enqueue(now.DateTime);
                        
                        // For private endpoints, increment our counter before sending
                        if (!isPublicEndpoint)
                        {
                            CheckAndResetPrivateWindow();
                            _options.Value.PrivateRequestsMadeInWindow++;
                        }
                        
                        // Use a timeout to prevent hanging
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
                        }
                        
                        response = await base.SendAsync(request, timeoutCts.Token);
                        
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
                        var retryDelay = TimeSpan.FromMilliseconds(Math.Min(backoffDelayMs + jitter, 3000)); // Cap at 3 seconds
                        
                        // Use a timeout for the delay too
                        await Task.Delay(retryDelay, cancellationToken);
                        retryCount++;
                        
                        // Create a new request for retry (can't reuse the original)
                        var retryRequest = CopyRequest(request);
                        request = retryRequest;
                    }
                    catch (TaskCanceledException)
                    {
                        // If the task was canceled because of our timeout, treat it like a rate limit
                        _logger.LogWarning("Request timed out, treating as rate limit and retrying");
                        retryCount++;
                        
                        if (retryCount > MaxRetryCount)
                        {
                            throw new TimeoutException("Request timed out after multiple retries");
                        }
                        
                        // Create a new request for retry (can't reuse the original)
                        var retryRequest = CopyRequest(request);
                        request = retryRequest;
                        await Task.Delay(1000, cancellationToken);
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
                
                // For private endpoints, update our tracking with server information
                if (!isPublicEndpoint && response != null)
                {
                    UpdatePrivateEndpointRateLimitState(response, isOrderManagementEndpoint);
                }
                
                return response!;
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException("Request timed out");
            }
        }
        
        private async Task EnforceGlobalHourlyRateLimit(DateTimeOffset now, CancellationToken cancellationToken)
        {
            // Get remaining hourly requests
            int remainingHourly = EstimatedRemainingHourlyRequests(now);
            
            // Calculate how aggressive our throttling should be based on remaining requests
            // When we have a lot remaining, we can be less strict
            // As we get closer to the limit, we get more conservative
            double requestsPerSecondTarget = remainingHourly / 3600.0;
            int targetIntervalMs = (int)(1000 / Math.Max(0.1, requestsPerSecondTarget));
            
            // Get the most recent request timestamp
            if (_options.Value.HourlyRequestsTimestamps.TryPeek(out DateTime lastRequest))
            {
                // Calculate time since last request
                var timeSinceLastRequest = (now - lastRequest).TotalMilliseconds;
                
                // If we haven't waited long enough, delay
                if (timeSinceLastRequest < targetIntervalMs)
                {
                    // Calculate delay needed
                    int delayMs = (int)(targetIntervalMs - timeSinceLastRequest);
                    
                    // Only delay if it's significant, and cap at 1 second to avoid hanging tests
                    if (delayMs > 10)
                    {
                        await Task.Delay(Math.Min(delayMs, 1000), cancellationToken);
                    }
                }
            }
        }
        
        private void CheckAndResetPrivateWindow()
        {
            var now = _getCurrentTime();
            if (now >= _options.Value.PrivateRateLimitResetTime)
            {
                _options.Value.PrivateRequestsMadeInWindow = 0;
                _options.Value.PrivateRateLimitResetTime = now.AddMinutes(1);
            }
        }

        private async Task EnforcePrivateEndpointRateLimit(bool isOrderManagement, CancellationToken cancellationToken)
        {
            // Check if the rate limit window has reset
            CheckAndResetPrivateWindow();
            
            // Use the appropriate limit based on endpoint type
            int limitToEnforce = isOrderManagement 
                ? _options.Value.OrderManagementEndpointRateLimit 
                : _options.Value.PrivateEndpointRateLimit;
            
            // Calculate how many requests we think we have remaining
            int estimatedRemaining = Math.Max(0, limitToEnforce - _options.Value.PrivateRequestsMadeInWindow);
            
            // If our estimate shows only 1 or 0 remaining, apply throttling
            if (estimatedRemaining <= 1)
            {
                // Small delay to avoid hitting the limit - silent operation
                // Cap at 500ms to avoid hanging tests
                await Task.Delay(Math.Min(LowRemainingDelayMs, 500), cancellationToken);
            }
        }
        
        private async Task EnforcePublicEndpointRateLimit(CancellationToken cancellationToken)
        {
            // Clean up timestamps older than 1 second
            while (_options.Value.RecentPublicRequests.TryPeek(out DateTime oldestTime) && 
                  (DateTime.UtcNow - oldestTime).TotalSeconds > 1)
            {
                _options.Value.RecentPublicRequests.TryDequeue(out _);
            }
            
            // If we've reached the limit of public requests per second, wait silently
            if (_options.Value.RecentPublicRequests.Count >= _options.Value.PublicEndpointRateLimit)
            {
                // Small delay to spread requests - silent operation
                // Cap at 500ms to avoid hanging tests
                await Task.Delay(Math.Min(100, 500), cancellationToken);
            }
            
            // Record this request timestamp
            _options.Value.RecentPublicRequests.Enqueue(DateTime.UtcNow);
        }
        
        private void UpdatePrivateEndpointRateLimitState(HttpResponseMessage response, bool isOrderManagement)
        {
            // Extract and parse the rate limit headers
            bool updatedValues = false;
            
            // Parse the rate limit (may contain complex values)
            if (response.Headers.TryGetValues("x-ratelimit-limit", out var limitValues) &&
                limitValues.FirstOrDefault() is string limitValue)
            {
                ParseAndUpdateRateLimit(limitValue, isOrderManagement);
            }
            
            // Get remaining requests
            if (response.Headers.TryGetValues("x-ratelimit-remaining", out var remainingValues) &&
                remainingValues.FirstOrDefault() is string remainingValue &&
                int.TryParse(remainingValue, out int remaining))
            {
                _options.Value.LastRemainingPrivateRequests = remaining;
                
                // Get the appropriate limit based on endpoint type
                int currentLimit = isOrderManagement 
                    ? _options.Value.OrderManagementEndpointRateLimit 
                    : _options.Value.PrivateEndpointRateLimit;
                
                // Sync our counter with the server's value
                _options.Value.PrivateRequestsMadeInWindow = currentLimit - remaining;
                updatedValues = true;
            }
            
            // Get reset time
            if (response.Headers.TryGetValues("x-ratelimit-reset", out var resetValues) &&
                resetValues.FirstOrDefault() is string resetValue)
            {
                // Try to parse the reset value (could be seconds or a timestamp)
                if (long.TryParse(resetValue, out long resetTimestamp))
                {
                    // If it's a small number, it's probably seconds until reset
                    if (resetTimestamp < 1000000)  // Arbitrary cutoff for "small" value
                    {
                        _options.Value.PrivateRateLimitResetTime = DateTimeOffset.UtcNow.AddSeconds(resetTimestamp);
                    }
                    else
                    {
                        // Otherwise it's a Unix timestamp
                        _options.Value.PrivateRateLimitResetTime = DateTimeOffset.FromUnixTimeSeconds(resetTimestamp);
                    }
                    
                    _options.Value.HasReceivedResetTime = true;
                    updatedValues = true;
                }
            }
            
            // If we received updated values and they're drastically different from our tracking,
            // log a debug message about the synchronization
            if (updatedValues)
            {
                int currentLimit = isOrderManagement 
                    ? _options.Value.OrderManagementEndpointRateLimit 
                    : _options.Value.PrivateEndpointRateLimit;
                
                int expectedUsed = currentLimit - _options.Value.LastRemainingPrivateRequests;
                
                if (Math.Abs(_options.Value.PrivateRequestsMadeInWindow - expectedUsed) > 2)
                {
                    _logger.LogDebug("Synced rate limit tracking: Our count={OurCount}, Server reports={ServerCount}",
                        _options.Value.PrivateRequestsMadeInWindow, expectedUsed);
                }
            }
        }
        
        private void ParseAndUpdateRateLimit(string limitValue, bool isOrderManagement)
        {
            try
            {
                // Extract the first number from the rate limit string
                var match = RateLimitPattern.Match(limitValue);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int firstLimit))
                {
                    // Determine if this is a complex rate limit header
                    bool isComplexHeader = limitValue.Contains(';') || limitValue.Contains(',');
                    
                    if (isComplexHeader)
                    {
                        // For complex headers, we need to determine endpoint-specific vs. global limits
                        if (isOrderManagement)
                        {
                            // For order management, use the first value which is the endpoint-specific limit
                            _options.Value.OrderManagementEndpointRateLimit = firstLimit;
                            
                            // Look for global limit as well
                            if (limitValue.Contains("global_limit") && 
                                Regex.Match(limitValue, @"(\d+);w=1;name=""global_limit""").Groups[1].Success &&
                                int.TryParse(Regex.Match(limitValue, @"(\d+);w=1;name=""global_limit""").Groups[1].Value, out int globalLimit))
                            {
                                _options.Value.PrivateEndpointRateLimit = globalLimit;
                            }
                        }
                        else
                        {
                            // For other endpoints, use the first value or fall back to default (30)
                            _options.Value.PrivateEndpointRateLimit = firstLimit;
                        }
                    }
                    else
                    {
                        // Simple header - use the value directly
                        if (isOrderManagement)
                        {
                            _options.Value.OrderManagementEndpointRateLimit = firstLimit;
                        }
                        else
                        {
                            _options.Value.PrivateEndpointRateLimit = firstLimit;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail if we can't parse the rate limit
                _logger.LogWarning(ex, "Failed to parse rate limit header: {LimitHeader}", limitValue);
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
            
            // Also log hourly limit status
            int hourlyRemaining = EstimatedRemainingHourlyRequests(DateTimeOffset.UtcNow);
            
            _logger.LogWarning("{EndpointType} endpoint rate limit exceeded for {Url}. Per-Second: Limit={Limit}, Remaining={Remaining}, Reset={Reset}. Global: {HourlyRemaining} of {HourlyLimit} hourly requests remaining.",
                endpointType, requestUri, limitValue, remainingValue, resetValue, hourlyRemaining, _options.Value.GlobalHourlyRateLimit);
        }
        
        private string GetHeaderValue(HttpResponseMessage response, string headerName)
        {
            if (response.Headers.TryGetValues(headerName, out var values))
            {
                return values.FirstOrDefault() ?? string.Empty;
            }
            return string.Empty;
        }
        
        private int EstimatedRemainingHourlyRequests(DateTimeOffset now)
        {
            // Remove timestamps older than 1 hour
            DateTime oldestTime;
            while (_options.Value.HourlyRequestsTimestamps.Count > 0 &&
                   _options.Value.HourlyRequestsTimestamps.TryPeek(out oldestTime) &&
                   oldestTime < now.AddHours(-1))
            {
                _options.Value.HourlyRequestsTimestamps.TryDequeue(out _);
            }
            
            return _options.Value.HourlyRequestsTarget - _options.Value.HourlyRequestsTimestamps.Count;
        }
    }
} 