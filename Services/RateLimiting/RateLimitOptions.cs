using System.Collections.Concurrent;

namespace crypto_bot_api.Services.RateLimiting
{
    public class RateLimitOptions
    {
        // Public endpoint limit - set to 9 instead of 10 to avoid inconsistent throttling
        public int PublicEndpointRateLimit { get; set; } = 9;
        
        // Private endpoint cap - using the full 30 requests per second limit
        public int PrivateEndpointRateLimit { get; set; } = 30;
        
        // Track recent public requests to enforce the rate limit
        public ConcurrentQueue<DateTime> RecentPublicRequests { get; } = new();
        
        // Track the last observed remaining request count for private endpoints
        public int LastRemainingPrivateRequests { get; set; } = 30;
    }
} 