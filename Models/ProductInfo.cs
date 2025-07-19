using System.ComponentModel.DataAnnotations;

namespace crypto_bot_api.Models
{
    public class ProductInfo
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string ProductId { get; set; } = string.Empty;

        [Required]
        public string BaseCurrency { get; set; } = string.Empty;

        [Required]
        public string QuoteCurrency { get; set; } = string.Empty;

        public decimal QuoteIncrement { get; set; }

        public decimal BaseIncrement { get; set; }

        [Required]
        public string DisplayName { get; set; } = string.Empty;

        public decimal MinMarketFunds { get; set; }

        public bool MarginEnabled { get; set; }

        public bool PostOnly { get; set; }

        public bool LimitOnly { get; set; }

        public bool CancelOnly { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;

        [Required]
        public string StatusMessage { get; set; } = string.Empty;

        public bool TradingDisabled { get; set; }

        public bool FxStablecoin { get; set; }

        public decimal MaxSlippagePercentage { get; set; }

        public bool AuctionMode { get; set; }

        [Required]
        public string HighBidLimitPercentage { get; set; } = string.Empty;

        public DateTime LastUpdated { get; set; }
    }
} 