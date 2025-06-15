namespace crypto_bot_api.Models
{
    public class FinalizedOrderDetails
    {
        public string? Order_Id { get; set; }
        public string? Trade_Id { get; set; }
        public string? Trade_Type { get; set; }
        public DateTime? Acquired_Time { get; set; }
        public decimal? Acquired_Price { get; set; }
        public decimal? Acquired_Quantity { get; set; }
        public decimal? Initial_Size { get; set; }
        public decimal? Commissions { get; set; }
        public string? Asset_Pair { get; set; }
        public string? Status { get; set; }
    }
} 