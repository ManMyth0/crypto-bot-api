using System;
using System.Collections.Concurrent;

namespace crypto_bot_api.Services.RateLimiting
{
    public class RateLimitOptions
    {
        // Default rate limits from Coinbase Advanced API docs
        public int PublicEndpointRateLimit { get; set; } = 10;
        public int PrivateEndpointRateLimit { get; set; } = 30;
        public int OrderManagementEndpointRateLimit { get; set; } = 10;
        public int GlobalHourlyRateLimit { get; set; } = 10000;
        
        // Target slightly below the limit to give minimal buffer
        public int HourlyRequestsTarget => GlobalHourlyRateLimit - 5;
        
        // Tracking for rate limits
        public ConcurrentQueue<DateTime> HourlyRequestsTimestamps { get; } = new ConcurrentQueue<DateTime>();
        public ConcurrentQueue<DateTime> RecentPublicRequests { get; } = new ConcurrentQueue<DateTime>();
        
        // Private requests in current window
        public int PrivateRequestsMadeInWindow { get; set; } = 0;
        public DateTimeOffset PrivateRateLimitResetTime { get; set; } = DateTimeOffset.UtcNow.AddMinutes(1);
        
        // Last known values from server
        public int LastRemainingPrivateRequests { get; set; } = 30;
        public bool HasReceivedResetTime { get; set; } = false;
        
        public int EstimatedRemainingHourlyRequests(DateTimeOffset now)
        {
            // Remove timestamps older than 1 hour
            DateTime oldestTime;
            while (HourlyRequestsTimestamps.Count > 0 &&
                   HourlyRequestsTimestamps.TryPeek(out oldestTime) &&
                   oldestTime < now.AddHours(-1))
            {
                HourlyRequestsTimestamps.TryDequeue(out _);
            }
            
            return HourlyRequestsTarget - HourlyRequestsTimestamps.Count;
        }
    }
} 