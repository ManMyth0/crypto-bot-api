using System.Text.Json.Serialization;

namespace crypto_bot_api.Models.DTOs.Orders
{
    public class ListOrderFillsRequestDto
    {
        [JsonPropertyName("order_id")]
        public string? OrderId { get; set; }

        [JsonPropertyName("order_ids")]
        public string[]? OrderIds { get; set; }

        [JsonPropertyName("trade_ids")]
        public string[]? TradeIds { get; set; }

        [JsonPropertyName("product_id")]
        public string? ProductId { get; set; }

        [JsonPropertyName("product_ids")]
        public string[]? ProductIds { get; set; }

        [JsonPropertyName("start_sequence_timestamp")]
        public string? StartSequenceTimestamp { get; set; }

        [JsonPropertyName("end_sequence_timestamp")]
        public string? EndSequenceTimestamp { get; set; }

        [JsonPropertyName("limit")]
        public int? Limit { get; set; } = 50; // Default to 50 as requested

        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; }

        [JsonPropertyName("sort_by")]
        public string? SortBy { get; set; }
    }
} 