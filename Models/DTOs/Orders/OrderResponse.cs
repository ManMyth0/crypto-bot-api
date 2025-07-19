using System.Text.Json.Nodes;

namespace crypto_bot_api.Models.DTOs.Orders
{
    public class OrderResponse
    {
        public JsonObject Order { get; set; } = new();
        public ValidationResult ValidationResult { get; set; } = new();
    }
} 