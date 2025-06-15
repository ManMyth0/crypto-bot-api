using System.Text.Json.Nodes;

namespace crypto_bot_api.Services
{
    public interface ITradeMetricsCalculator
    {
        decimal CalculateTotalCommission(JsonArray fills);
        decimal CalculateTotalPrice(JsonArray fills);
        decimal CalculateTotalSize(JsonArray fills);
    }

    public class TradeMetricsCalculator : ITradeMetricsCalculator
    {
        public decimal CalculateTotalCommission(JsonArray fills)
        {
            decimal totalCommission = 0m;
            foreach (var fillItem in fills)
            {
                if (fillItem?.AsObject() is JsonObject fill)
                {
                    if (decimal.TryParse(fill["commission"]?.GetValue<string>(), out decimal commission))
                    {
                        totalCommission += commission;
                    }
                }
            }
            return totalCommission;
        }

        public decimal CalculateTotalPrice(JsonArray fills)
        {
            decimal totalPrice = 0m;
            decimal totalSize = 0m;
            foreach (var fillItem in fills)
            {
                if (fillItem?.AsObject() is JsonObject fill)
                {
                    if (decimal.TryParse(fill["price"]?.GetValue<string>(), out decimal price) &&
                        decimal.TryParse(fill["size"]?.GetValue<string>(), out decimal size))
                    {
                        totalPrice += price * size;
                        totalSize += size;
                    }
                }
            }
            return totalSize > 0 ? totalPrice / totalSize : 0m;
        }

        public decimal CalculateTotalSize(JsonArray fills)
        {
            decimal totalSize = 0m;
            foreach (var fillItem in fills)
            {
                if (fillItem?.AsObject() is JsonObject fill)
                {
                    if (decimal.TryParse(fill["size"]?.GetValue<string>(), out decimal size))
                    {
                        totalSize += size;
                    }
                }
            }
            return totalSize;
        }
    }
} 