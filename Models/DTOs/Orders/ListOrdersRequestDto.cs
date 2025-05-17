using System.Text.Json.Serialization;

namespace crypto_bot_api.Models.DTOs.Orders
{
    public class ListOrdersRequestDto
    {
        [JsonPropertyName("order_ids")]
        public string[]? OrderIds { get; set; }

        [JsonPropertyName("product_ids")]
        public string[]? ProductIds { get; set; }

        [JsonPropertyName("product_type")]
        public string? ProductType { get; set; }

        [JsonPropertyName("order_status")]
        public string[]? OrderStatus { get; set; }

        [JsonPropertyName("time_in_forces")]
        public string[]? TimeInForces { get; set; }

        [JsonPropertyName("order_types")]
        public string[]? OrderTypes { get; set; }

        [JsonPropertyName("order_side")]
        public string? OrderSide { get; set; }

        [JsonPropertyName("start_date")]
        public string? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        public string? EndDate { get; set; }

        [JsonPropertyName("order_placement_source")]
        public string? OrderPlacementSource { get; set; }

        [JsonPropertyName("contract_expiry_type")]
        public string? ContractExpiryType { get; set; }

        [JsonPropertyName("asset_filters")]
        public string[]? AssetFilters { get; set; }

        [JsonPropertyName("retail_portfolio_id")]
        public string? RetailPortfolioId { get; set; }

        [JsonPropertyName("limit")]
        public int? Limit { get; set; } = 50; // Default to 50 like with fills

        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; }

        [JsonPropertyName("sort_by")]
        public string? SortBy { get; set; }
    }
} 