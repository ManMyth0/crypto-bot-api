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
        Task<ProductInfo?> GetProductInfoAsync(string productId);
        Task RefreshProductInfoAsync();
        Task<DateTime> GetLastUpdateTimeAsync(string productId);
    }

    public class ProductInfoService : IProductInfoService
    {
        private readonly AppDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductInfoService> _logger;
        private const string COINBASE_PRODUCTS_URL = "https://api.coinbase.com/api/v3/brokerage/products";
        private static readonly TimeSpan REFRESH_THRESHOLD = TimeSpan.FromHours(24);
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true // This will handle both camelCase and snake_case
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
            else if (DateTime.UtcNow - productInfo.LastUpdated > REFRESH_THRESHOLD)
            {
                _logger.LogInformation($"Product info for {productId} is older than {REFRESH_THRESHOLD.TotalHours} hours. Refreshing from Coinbase.");
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
            try
            {
                var response = await _httpClient.GetAsync(COINBASE_PRODUCTS_URL);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var productsResponse = JsonSerializer.Deserialize<CoinbaseProductsResponse>(content, _jsonOptions);

                if (productsResponse?.Products == null || !productsResponse.Products.Any())
                {
                    throw new InvalidOperationException("No products returned from Coinbase API");
                }

                var now = DateTime.UtcNow;
                foreach (var product in productsResponse.Products)
                {
                    if (string.IsNullOrEmpty(product.Id))
                    {
                        _logger.LogWarning("Skipping product with null or empty ID");
                        continue;
                    }

                    var existingProduct = await _dbContext.ProductInfo
                        .FirstOrDefaultAsync(p => p.ProductId == product.Id);

                    if (existingProduct != null)
                    {
                        existingProduct.BaseCurrency = product.BaseCurrency ?? string.Empty;
                        existingProduct.QuoteCurrency = product.QuoteCurrency ?? string.Empty;
                        existingProduct.BaseIncrement = ParseDecimalOrDefault(product.BaseIncrement);
                        existingProduct.QuoteIncrement = ParseDecimalOrDefault(product.QuoteIncrement);
                        existingProduct.MinMarketFunds = ParseDecimalOrDefault(product.MinMarketFunds);
                        existingProduct.DisplayName = product.DisplayName ?? string.Empty;
                        existingProduct.MarginEnabled = product.MarginEnabled;
                        existingProduct.PostOnly = product.PostOnly;
                        existingProduct.LimitOnly = product.LimitOnly;
                        existingProduct.CancelOnly = product.CancelOnly;
                        existingProduct.Status = product.Status ?? string.Empty;
                        existingProduct.StatusMessage = product.StatusMessage ?? string.Empty;
                        existingProduct.TradingDisabled = product.TradingDisabled;
                        existingProduct.FxStablecoin = product.FxStablecoin;
                        existingProduct.MaxSlippagePercentage = ParseDecimalOrDefault(product.MaxSlippagePercentage);
                        existingProduct.AuctionMode = product.AuctionMode;
                        existingProduct.HighBidLimitPercentage = product.HighBidLimitPercentage ?? string.Empty;
                        existingProduct.LastUpdated = now;
                    }
                    else
                    {
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
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"Successfully refreshed product information from Coinbase at {now}");
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

        private class CoinbaseProductsResponse
        {
            public List<CoinbaseProduct>? Products { get; set; }
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