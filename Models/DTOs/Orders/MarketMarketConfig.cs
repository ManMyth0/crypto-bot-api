using System.Text.Json.Serialization;

namespace crypto_bot_api.Models.DTOs.Orders
{
    public class MarketMarketConfig
    {
        [JsonPropertyName("quote_size")]
        public string? QuoteSize { get; set; }

        [JsonPropertyName("base_size")]
        public string? BaseSize { get; set; }
    }
} 