namespace crypto_bot_api.Utilities
{
    public class PercentageOfReturn
    {
        // Method to calculate percentage-based profit/loss return
        public static decimal? Calculate(decimal? acquiredPrice, decimal? soldPrice, string tradeType)
        {
            if (acquiredPrice == null || soldPrice == null || string.IsNullOrEmpty(tradeType))
            {
                // Return null if any values are missing
                return null; 
            }

            // If the result is negative, then it is a loss, otherwise it is break-even or a profit
            decimal result;

            if (string.Equals(tradeType, "SHORT", StringComparison.OrdinalIgnoreCase))
            {
                result = ((acquiredPrice.Value - soldPrice.Value) / acquiredPrice.Value) * 100;
            }
            else if (string.Equals(tradeType, "LONG", StringComparison.OrdinalIgnoreCase))
            {
                result = ((soldPrice.Value - acquiredPrice.Value) / acquiredPrice.Value) * 100;
            }
            else
            {
                // Invalid trade type
                return null; 
            }

            return result;
        }
    }
}