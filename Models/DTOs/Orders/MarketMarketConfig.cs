using System.Text.Json.Serialization;

namespace crypto_bot_api.Models.DTOs.Orders
{
    public class MarketMarketIocConfig
    {
        [JsonPropertyName("quote_size")]
        public string? QuoteSize { get; set; }

        [JsonPropertyName("base_size")]
        public string? BaseSize { get; set; }

        [JsonPropertyName("rfq_disabled")]
        public bool RfqDisabled { get; set; } = true;
    }
} 