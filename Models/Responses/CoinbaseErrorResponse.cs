using System.Text.Json.Serialization;

namespace crypto_bot_api.Models.Responses
{
    public class CoinbaseErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
        
        [JsonPropertyName("error_response")]
        public string? ErrorResponse { get; set; }
        
        [JsonPropertyName("error_details")]
        public string? ErrorDetailsText { get; set; }
        
        public string? Code { get; set; }
        
        // Additional nested error models
        public ErrorDetailsInfo? Details { get; set; }
        
        public class ErrorDetailsInfo
        {
            public string? Code { get; set; }
            public string? Message { get; set; }
            public string? Description { get; set; }
        }
    }
} 