namespace crypto_bot_api.Models
{
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Warnings { get; set; } = new();
        public DateTime ValidationTimestamp { get; set; }
        public string? ProductId { get; set; }
        public string? Status { get; set; }
        public bool TradingDisabled { get; set; }
    }
} 