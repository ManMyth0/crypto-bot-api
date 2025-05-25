namespace crypto_bot_api.Models
{
    public class FinalizedOrderDetails
    {
        public string OrderId { get; set; } = string.Empty;
        public string TradeId { get; set; } = string.Empty;
        public string EntryId { get; set; } = string.Empty;
        public DateTime TradeTime { get; set; }
        public string TradeType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public decimal TotalCommission { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public DateTime SequenceTimestamp { get; set; }
        public string LiquidityIndicator { get; set; } = string.Empty;
        public bool SizeInQuote { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public string RetailPortfolioId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
} 