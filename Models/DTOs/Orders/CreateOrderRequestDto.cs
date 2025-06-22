using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace crypto_bot_api.Models.DTOs.Orders
{
    public class CreateOrderRequestDto
    {
        [JsonPropertyName("client_order_id")]
        [Required(ErrorMessage = "client_order_id is required")]
        public string ClientOrderId { get; set; } = string.Empty;

        [JsonPropertyName("product_id")]
        [Required(ErrorMessage = "product_id is required")]
        public string ProductId { get; set; } = string.Empty;

        [JsonPropertyName("side")]
        [Required(ErrorMessage = "side is required")]
        [RegularExpression("BUY|SELL", ErrorMessage = "side must be either 'BUY' or 'SELL'")]
        public string Side { get; set; } = string.Empty;

        [JsonPropertyName("position_type")]
        [Required(ErrorMessage = "position_type is required")]
        [RegularExpression("(?i)^(LONG|SHORT)$", ErrorMessage = "position_type must be either 'LONG' or 'SHORT' (case insensitive)")]
        public string PositionType { get; set; } = string.Empty;

        [JsonPropertyName("order_configuration")]
        [Required(ErrorMessage = "order_configuration is required")]
        public OrderConfigurationDto OrderConfiguration { get; set; } = new OrderConfigurationDto();

        // Helper method to get normalized position type
        public string GetNormalizedPositionType() => PositionType.ToUpperInvariant();
    }

    public class OrderConfigurationDto
    {
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
        public string LimitPrice { get; set; } = string.Empty;

        [JsonPropertyName("post_only")]
        public bool PostOnly { get; set; }
    }

    public class LimitLimitGtdDto
    {
        [JsonPropertyName("quote_size")]
        public string? QuoteSize { get; set; }

        [JsonPropertyName("base_size")]
        public string? BaseSize { get; set; }

        [JsonPropertyName("limit_price")]
        public string LimitPrice { get; set; } = string.Empty;

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; } = string.Empty; // RFC3339 Timestamp

        [JsonPropertyName("post_only")]
        public bool PostOnly { get; set; }
    }
} 