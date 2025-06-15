using System.Text.Json.Nodes;

namespace crypto_bot_api.Utilities
{
    public class TradeMetricsCalculator
    {
        // Calculates the total commission from a collection of fills
        public static decimal CalculateTotalCommission(JsonArray fills)
        {
            decimal totalCommission = 0;

            foreach (var fill in fills)
            {
                if (fill == null) continue;

                var commission = fill["commission"]?.GetValue<string>();
                if (decimal.TryParse(commission, out decimal fillCommission))
                {
                    totalCommission += fillCommission;
                }
            }

            return totalCommission;
        }

        // Calculates the total price (total amount spent/received) from all fills
        public static decimal CalculateTotalPrice(JsonArray fills)
        {
            decimal totalPrice = 0;

            foreach (var fill in fills)
            {
                if (fill == null) continue;

                var price = fill["price"]?.GetValue<string>();
                var size = fill["size"]?.GetValue<string>();

                if (decimal.TryParse(price, out decimal fillPrice) && 
                    decimal.TryParse(size, out decimal fillSize))
                {
                    totalPrice += fillPrice * fillSize;
                }
            }

            return totalPrice;
        }

        // Calculates the total size (quantity) from all fills
        public static decimal CalculateTotalSize(JsonArray fills)
        {
            decimal totalSize = 0;

            foreach (var fill in fills)
            {
                if (fill == null) continue;

                var size = fill["size"]?.GetValue<string>();
                if (decimal.TryParse(size, out decimal fillSize))
                {
                    totalSize += fillSize;
                }
            }

            return totalSize;
        }
    }
} 