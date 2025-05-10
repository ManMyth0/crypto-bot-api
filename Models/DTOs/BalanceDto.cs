using System.Text.Json.Serialization;

namespace crypto_bot_api.Models.DTOs
{
    public class BalanceDto
    {
        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }
    }
} 