using System.Text.Json.Nodes;

namespace crypto_bot_api.Services
{
    public interface ITradeMetricsCalculator
    {
        decimal CalculateTotalCommission(JsonArray fills);
        decimal CalculateTotalPrice(JsonArray fills);
        decimal CalculateTotalSize(JsonArray fills);
        decimal CalculateProfitLoss(bool isLong, decimal quantity, decimal entryPrice, decimal exitPrice);
        decimal CalculatePercentageReturn(bool isLong, decimal entryPrice, decimal exitPrice);
    }

    public class TradeMetricsCalculator : ITradeMetricsCalculator
    {
        public decimal CalculateTotalCommission(JsonArray fills)
        {
            decimal totalCommission = 0m;
            foreach (var fill in fills)
            {
                var commissionStr = fill?["commission"]?.GetValue<string>();
                if (decimal.TryParse(commissionStr, out decimal commission))
                {
                    totalCommission += commission;
                }
            }
            return totalCommission;
        }

        public decimal CalculateTotalPrice(JsonArray fills)
        {
            decimal totalPrice = 0m;
            decimal totalSize = 0m;

            foreach (var fill in fills)
            {
                var priceStr = fill?["price"]?.GetValue<string>();
                var sizeStr = fill?["size"]?.GetValue<string>();

                if (decimal.TryParse(priceStr, out decimal price) &&
                    decimal.TryParse(sizeStr, out decimal size))
                {
                    totalPrice += price * size;
                    totalSize += size;
                }
            }

            return totalSize > 0 ? totalPrice / totalSize : 0m;
        }

        public decimal CalculateTotalSize(JsonArray fills)
        {
            decimal totalSize = 0m;
            foreach (var fill in fills)
            {
                var sizeStr = fill?["size"]?.GetValue<string>();
                if (decimal.TryParse(sizeStr, out decimal size))
                {
                    totalSize += size;
                }
            }
            return totalSize;
        }

        public decimal CalculateProfitLoss(bool isLong, decimal quantity, decimal entryPrice, decimal exitPrice)
        {
            if (isLong)
            {
                return (exitPrice - entryPrice) * quantity;
            }
            else
            {
                return (entryPrice - exitPrice) * quantity;
            }
        }

        public decimal CalculatePercentageReturn(bool isLong, decimal entryPrice, decimal exitPrice)
        {
            if (entryPrice == 0) return 0m;

            if (isLong)
            {
                return ((exitPrice - entryPrice) / entryPrice) * 100m;
            }
            else
            {
                return ((entryPrice - exitPrice) / entryPrice) * 100m;
            }
        }
    }
} 