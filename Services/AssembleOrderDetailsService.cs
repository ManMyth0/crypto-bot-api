using System.Text.Json.Nodes;
using crypto_bot_api.Models;

namespace crypto_bot_api.Services
{
    public interface IAssembleOrderDetailsService
    {
        FinalizedOrderDetails? AssembleFromFills(string orderId, JsonArray? fills, string? terminalStatus = null, decimal? initialSize = null);
    }

    public class AssembleOrderDetailsService : IAssembleOrderDetailsService
    {
        private readonly TradeMetricsCalculator _calculator = new();

        public FinalizedOrderDetails? AssembleFromFills(string orderId, JsonArray? fills, string? terminalStatus = null, decimal? initialSize = null)
        {
            if (fills == null || fills.Count == 0)
            {
                return new FinalizedOrderDetails
                {
                    Order_Id = orderId,
                    Status = terminalStatus ?? string.Empty,
                    Trade_Id = string.Empty,
                    Trade_Type = string.Empty,
                    Asset_Pair = string.Empty,
                    Initial_Size = initialSize,
                    Commissions = 0m,
                    Acquired_Quantity = 0m
                };
            }

            var firstFill = fills[0]?.AsObject();
            if (firstFill == null)
            {
                return new FinalizedOrderDetails
                {
                    Order_Id = orderId,
                    Status = terminalStatus ?? string.Empty,
                    Trade_Id = string.Empty,
                    Trade_Type = string.Empty,
                    Asset_Pair = string.Empty,
                    Initial_Size = initialSize,
                    Commissions = 0m,
                    Acquired_Quantity = 0m
                };
            }

            var details = new FinalizedOrderDetails
            {
                Order_Id = orderId,
                Status = terminalStatus ?? "FILLED",
                Trade_Id = firstFill["trade_id"]?.GetValue<string>() ?? string.Empty,
                Trade_Type = firstFill["side"]?.GetValue<string>() ?? string.Empty,
                Asset_Pair = firstFill["product_id"]?.GetValue<string>() ?? string.Empty,
                Initial_Size = initialSize,
                Commissions = _calculator.CalculateTotalCommission(fills),
                Acquired_Price = _calculator.CalculateTotalPrice(fills),
                Acquired_Quantity = _calculator.CalculateTotalSize(fills)
            };

            // Parse DateTime
            var tradeTimeStr = firstFill["trade_time"]?.GetValue<string>();
            if (tradeTimeStr != null && DateTime.TryParse(tradeTimeStr, out var tradeTime))
            {
                // Ensure the DateTime is treated as UTC to avoid PostgreSQL timezone issues
                details.Acquired_Time = DateTime.SpecifyKind(tradeTime, DateTimeKind.Utc);
            }

            return details;
        }
    }
} 