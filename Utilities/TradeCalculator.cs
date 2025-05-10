namespace crypto_bot_api.Utilities
{
    public class TradeCalculator
    {
        // Method to perform Profit / Loss calculations based on trade type
        public static string? CalculateProfitLoss(decimal? acquiredPrice, decimal? soldPrice, decimal? acquiredQuantity, string tradeType)
        {
            if (acquiredPrice == null || soldPrice == null || acquiredQuantity == null || string.IsNullOrEmpty(tradeType))
            {
                // Return null if any of the values are missing
                return null; 
            }

            decimal result;

            // Case-insensitive comparison using to determine trade type
            if (string.Equals(tradeType, "SHORT", StringComparison.OrdinalIgnoreCase))
            {
                // Profit when soldPrice is lower
                result = acquiredPrice.Value - soldPrice.Value; 
            }
            else if (string.Equals(tradeType, "LONG", StringComparison.OrdinalIgnoreCase))
            {
                // Profit when soldPrice is higher
                result = soldPrice.Value - acquiredPrice.Value; 
            }
            else
            {
                // Failsafe for unexpected input
                return "Invalid Trade Type"; 
            }

            // Return trade classification based on result
            return result > 0 ? "Profit" : result < 0 ? "Loss" : "Break-even";
        }
    }
}