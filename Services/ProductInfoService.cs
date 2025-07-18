using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using crypto_bot_api.Data;
using crypto_bot_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace crypto_bot_api.Services
{
    public interface IProductInfoService
    {
        Task InitializeAsync();
        Task<ProductInfo?> GetProductInfoAsync(string productId);
        Task<DateTime> GetLastUpdateTimeAsync(string productId);
        Task RefreshProductInfoAsync();
    }

    public class ProductInfoService : IProductInfoService
    {
        private readonly AppDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductInfoService> _logger;
        private const string COINBASE_PRODUCTS_URL = "https://api.exchange.coinbase.com/products";
        private const int RETRY_DELAY_MS = 5000; // 5 seconds
        private const int MAX_RETRIES = 3;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ProductInfoService(
            AppDbContext dbContext,
            HttpClient httpClient,
            ILogger<ProductInfoService> logger)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            for (int attempt = 0; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    await RefreshProductInfoAsync();
                    _logger.LogInformation("Successfully initialized product information");
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == MAX_RETRIES)
                    {
                        _logger.LogError(ex, "Failed to initialize product information after {RetryCount} attempts", MAX_RETRIES);
                        return; // Continue API startup despite failure
                    }

                    _logger.LogWarning(ex, "Failed to initialize product information (Attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms...",
                        attempt + 1, MAX_RETRIES, RETRY_DELAY_MS);
                    await Task.Delay(RETRY_DELAY_MS);
                }
            }
        }

        public async Task<ProductInfo?> GetProductInfoAsync(string productId)
        {
            var productInfo = await _dbContext.ProductInfo
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (productInfo == null)
            {
                _logger.LogWarning($"Product info not found for {productId}. Attempting to refresh from Coinbase.");
                await RefreshProductInfoAsync();
                productInfo = await _dbContext.ProductInfo
                    .FirstOrDefaultAsync(p => p.ProductId == productId);
            }
            else if (DateTime.UtcNow - productInfo.LastUpdated > TimeSpan.FromHours(24))
            {
                _logger.LogInformation($"Product info for {productId} is older than 24 hours. Refreshing from Coinbase.");
                try
                {
                    await RefreshProductInfoAsync();
                    var refreshedInfo = await _dbContext.ProductInfo
                        .FirstOrDefaultAsync(p => p.ProductId == productId);
                    if (refreshedInfo != null)
                    {
                        productInfo = refreshedInfo;
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to find refreshed product info for {productId} after refresh. Using cached data from {productInfo.LastUpdated}.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to refresh product info for {productId}. Using cached data from {productInfo.LastUpdated}.");
                }
            }

            return productInfo;
        }

        public async Task<DateTime> GetLastUpdateTimeAsync(string productId)
        {
            var product = await _dbContext.ProductInfo
                .Where(p => p.ProductId == productId)
                .Select(p => p.LastUpdated)
                .FirstOrDefaultAsync();

            return product;
        }

        public async Task RefreshProductInfoAsync()
        {
            var response = await _httpClient.GetAsync(COINBASE_PRODUCTS_URL);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var products = JsonSerializer.Deserialize<List<CoinbaseProduct>>(content, _jsonOptions);

            if (products == null || !products.Any())
            {
                throw new InvalidOperationException("No products returned from Coinbase API");
            }

            var now = DateTime.UtcNow;
            try
            {
                // Clear existing products
                _dbContext.ProductInfo.RemoveRange(_dbContext.ProductInfo);

                // Add new products
                foreach (var product in products)
                {
                    if (string.IsNullOrEmpty(product.Id))
                    {
                        _logger.LogWarning("Skipping product with null or empty ID");
                        continue;
                    }

                    await _dbContext.ProductInfo.AddAsync(new ProductInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        ProductId = product.Id,
                        BaseCurrency = product.BaseCurrency ?? string.Empty,
                        QuoteCurrency = product.QuoteCurrency ?? string.Empty,
                        BaseIncrement = ParseDecimalOrDefault(product.BaseIncrement),
                        QuoteIncrement = ParseDecimalOrDefault(product.QuoteIncrement),
                        MinMarketFunds = ParseDecimalOrDefault(product.MinMarketFunds),
                        DisplayName = product.DisplayName ?? string.Empty,
                        MarginEnabled = product.MarginEnabled,
                        PostOnly = product.PostOnly,
                        LimitOnly = product.LimitOnly,
                        CancelOnly = product.CancelOnly,
                        Status = product.Status ?? string.Empty,
                        StatusMessage = product.StatusMessage ?? string.Empty,
                        TradingDisabled = product.TradingDisabled,
                        FxStablecoin = product.FxStablecoin,
                        MaxSlippagePercentage = ParseDecimalOrDefault(product.MaxSlippagePercentage),
                        AuctionMode = product.AuctionMode,
                        HighBidLimitPercentage = product.HighBidLimitPercentage ?? string.Empty,
                        LastUpdated = now
                    });
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Successfully refreshed product information at {Timestamp}", now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh product information from Coinbase");
                throw;
            }
        }

        private static decimal ParseDecimalOrDefault(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0m;
            return decimal.TryParse(value, out decimal result) ? result : 0m;
        }

        private class CoinbaseProduct
        {
            [JsonPropertyName("product_id")]
            public string? Id { get; set; }
            
            [JsonPropertyName("base_currency")]
            public string? BaseCurrency { get; set; }
            
            [JsonPropertyName("quote_currency")]
            public string? QuoteCurrency { get; set; }
            
            [JsonPropertyName("quote_increment")]
            public string? QuoteIncrement { get; set; }
            
            [JsonPropertyName("base_increment")]
            public string? BaseIncrement { get; set; }
            
            [JsonPropertyName("display_name")]
            public string? DisplayName { get; set; }
            
            [JsonPropertyName("min_market_funds")]
            public string? MinMarketFunds { get; set; }
            
            [JsonPropertyName("margin_enabled")]
            public bool MarginEnabled { get; set; }
            
            [JsonPropertyName("post_only")]
            public bool PostOnly { get; set; }
            
            [JsonPropertyName("limit_only")]
            public bool LimitOnly { get; set; }
            
            [JsonPropertyName("cancel_only")]
            public bool CancelOnly { get; set; }
            
            public string? Status { get; set; }
            
            [JsonPropertyName("status_message")]
            public string? StatusMessage { get; set; }
            
            [JsonPropertyName("trading_disabled")]
            public bool TradingDisabled { get; set; }
            
            [JsonPropertyName("fx_stablecoin")]
            public bool FxStablecoin { get; set; }
            
            [JsonPropertyName("max_slippage_percentage")]
            public string? MaxSlippagePercentage { get; set; }
            
            [JsonPropertyName("auction_mode")]
            public bool AuctionMode { get; set; }
            
            [JsonPropertyName("high_bid_limit_percentage")]
            public string? HighBidLimitPercentage { get; set; }
        }
    }
} 