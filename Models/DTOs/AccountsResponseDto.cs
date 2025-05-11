using System.Text.Json.Serialization;

namespace crypto_bot_api.Models.DTOs
{
    public class AccountsResponseDto
    {
        [JsonPropertyName("accounts")]
        public List<AccountDto>? Accounts { get; set; } = new List<AccountDto>();
    }
} 