using System.Text.Json.Serialization;

namespace crypto_bot_api.Models.DTOs.Orders
{
    public class ListOrderFillsRequestDto
    {
        [JsonPropertyName("order_id")]
        public string? OrderId { get; set; }

        [JsonPropertyName("product_id")]
        public string? ProductId { get; set; }

        [JsonPropertyName("start_sequence_timestamp")]
        public string? StartSequenceTimestamp { get; set; }

        [JsonPropertyName("end_sequence_timestamp")]
        public string? EndSequenceTimestamp { get; set; }

        [JsonPropertyName("limit")]
        public int? Limit { get; set; }

        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; }
    }
} 