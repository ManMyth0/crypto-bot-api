using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace crypto_bot_api.Models.DTOs.Orders
{
    public class CreateOrderRequestDto
    {
        [JsonPropertyName("client_order_id")]
        [Required]
        public string? ClientOrderId { get; set; }

        [JsonPropertyName("product_id")]
        [Required]
        public string? ProductId { get; set; }

        [JsonPropertyName("side")]
        [Required]
        [RegularExpression("BUY|SELL")]
        public string? Side { get; set; }

        [JsonPropertyName("position_type")]
        [Required]
        [RegularExpression("(?i)^(LONG|SHORT)$")]
        public string? PositionType { get; set; }

        [JsonPropertyName("order_configuration")]
        [Required]
        public OrderConfigurationDto? OrderConfiguration { get; set; }

        // Optional position ID for closing trades
        public Guid? PositionId { get; set; }

        public string GetNormalizedPositionType() => PositionType?.ToUpperInvariant() ?? string.Empty;
    }

    public class OrderConfigurationDto
    {
        [JsonPropertyName("market_market")]
        public MarketMarketConfig? MarketMarket { get; set; }

        [JsonPropertyName("limit_limit_gtc")]
        public LimitLimitGtcDto? LimitLimitGtc { get; set; }

        [JsonPropertyName("limit_limit_gtd")]
        public LimitLimitGtdDto? LimitLimitGtd { get; set; }
    }

    public class LimitLimitGtcDto
    {
        [JsonPropertyName("quote_size")]
        public string? QuoteSize { get; set; }

        [JsonPropertyName("base_size")]
        public string? BaseSize { get; set; }

        [JsonPropertyName("limit_price")]
        [Required]
        public string? LimitPrice { get; set; }
    }

    public class LimitLimitGtdDto
    {
        [JsonPropertyName("quote_size")]
        public string? QuoteSize { get; set; }

        [JsonPropertyName("base_size")]
        public string? BaseSize { get; set; }

        [JsonPropertyName("limit_price")]
        [Required]
        public string? LimitPrice { get; set; }

        [JsonPropertyName("end_time")]
        [Required]
        public string? EndTime { get; set; }
    }
} 