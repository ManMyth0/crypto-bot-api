using System.Text.Json.Serialization;

namespace crypto_bot_api.Models.DTOs
{
    public class AccountDetailResponseDto
    {
        [JsonPropertyName("account")]
        public AccountDto? Account { get; set; }
    }
} 