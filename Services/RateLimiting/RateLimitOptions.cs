using System.Collections.Concurrent;

namespace crypto_bot_api.Services.RateLimiting
{
    public class RateLimitOptions
    {
        // Public endpoint limit - set to 8 instead of 10 to avoid inconsistent throttling
        public int PublicEndpointRateLimit { get; set; } = 8;
        
        // Private endpoint cap - set to 29 instead of 30 to give a safety buffer
        public int PrivateEndpointRateLimit { get; set; } = 29;
        
        // Track recent public requests to enforce the rate limit
        public ConcurrentQueue<DateTime> RecentPublicRequests { get; } = new();
        
        // Track the last observed remaining request count for private endpoints
        public int LastRemainingPrivateRequests { get; set; } = 29;
    }
} 