namespace crypto_bot_api.Services.RateLimiting
{
    public static class CoinbaseRateLimitingExtensions
    {
        // Adds Coinbase API rate limiting services to the service collection
        public static IServiceCollection AddCoinbaseRateLimiting(this IServiceCollection services, 
            IConfiguration configuration)
        {
            // Configure rate limit options from configuration
            services.Configure<RateLimitOptions>(options => {
                // Allow configuration of public endpoint rate limit
                if (int.TryParse(configuration["CoinbaseApi:RateLimit:PublicEndpointRateLimit"], out int publicLimit))
                {
                    options.PublicEndpointRateLimit = publicLimit;
                }
                
                // Allow configuration of private endpoint rate limit
                if (int.TryParse(configuration["CoinbaseApi:RateLimit:PrivateEndpointRateLimit"], out int privateLimit))
                {
                    options.PrivateEndpointRateLimit = privateLimit;
                    options.LastRemainingPrivateRequests = privateLimit; // Initialize with the full limit
                }
            });
            
            // Register the rate limit handler
            services.AddTransient<CoinbaseRateLimitHandler>();
            
            return services;
        }
        

        // Adds an HTTP client with Coinbase rate limiting for a specific API client
        public static IHttpClientBuilder AddCoinbaseHttpClient<TClient, TImplementation>(
            this IServiceCollection services)
            where TClient : class
            where TImplementation : class, TClient
        {
            return services.AddHttpClient<TClient, TImplementation>()
                .AddHttpMessageHandler<CoinbaseRateLimitHandler>();
        }
    }
} 