using System.Text.Json.Serialization;

namespace crypto_bot_api.Models.DTOs.Orders
{
    public class CreateOrderResponseDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("success_response")]
        public SuccessResponseDto? SuccessResponse { get; set; }

        [JsonPropertyName("error_response")]
        public ErrorResponseDto? ErrorResponse { get; set; }

        [JsonPropertyName("order_configuration")]
        public OrderConfigurationResponseDto? OrderConfiguration { get; set; }
    }

    public class SuccessResponseDto
    {
        [JsonPropertyName("order_id")]
        public string? OrderId { get; set; }

        [JsonPropertyName("product_id")]
        public string? ProductId { get; set; }

        [JsonPropertyName("side")]
        public string? Side { get; set; }

        [JsonPropertyName("client_order_id")]
        public string? ClientOrderId { get; set; }
    }

    public class ErrorResponseDto
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error_details")]
        public string? ErrorDetails { get; set; }

        [JsonPropertyName("preview_failure_reason")]
        public string? PreviewFailureReason { get; set; }

        [JsonPropertyName("new_order_failure_reason")]
        public string? NewOrderFailureReason { get; set; }
    }

    public class OrderConfigurationResponseDto
    {
        [JsonPropertyName("limit_limit_gtc")]
        public LimitLimitGtcDto? LimitLimitGtc { get; set; }

        [JsonPropertyName("limit_limit_gtd")]
        public LimitLimitGtdDto? LimitLimitGtd { get; set; }
    }
} 