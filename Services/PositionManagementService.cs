using crypto_bot_api.Data;
using crypto_bot_api.Models;
using Microsoft.EntityFrameworkCore;

namespace crypto_bot_api.Services
{
    public interface IPositionManagementService
    {
        Task<TradeRecords> CreatePositionFromOrderAsync(FinalizedOrderDetails orderDetails, string? originalPositionType = null);
        Task<TradeRecords> UpdatePositionFromClosingOrderAsync(FinalizedOrderDetails orderDetails, Guid positionId);
        Task<TradeRecords> UpdatePositionFromClosingOrderAsync(FinalizedOrderDetails orderDetails);
        Task<IEnumerable<TradeRecords>> GetOpenPositionsAsync(string? assetPair = null);
        Task<TradeRecords?> GetPositionByIdAsync(Guid positionId);
    }

    public class PositionManagementService : IPositionManagementService
    {
        private readonly AppDbContext _context;
        private readonly TradeMetricsCalculator _calculator;

        public PositionManagementService(AppDbContext context, TradeMetricsCalculator calculator)
        {
            _context = context;
            _calculator = calculator;
        }

        public async Task<TradeRecords> CreatePositionFromOrderAsync(FinalizedOrderDetails orderDetails, string? originalPositionType = null)
        {
            if (string.IsNullOrEmpty(orderDetails.Trade_Type))
                throw new ArgumentException("Trade type is required", nameof(orderDetails));
            if (string.IsNullOrEmpty(orderDetails.Asset_Pair))
                throw new ArgumentException("Asset pair is required", nameof(orderDetails));
            if (!orderDetails.Acquired_Price.HasValue)
                throw new ArgumentException("Acquired price is required", nameof(orderDetails));
            if (!orderDetails.Acquired_Quantity.HasValue)
                throw new ArgumentException("Acquired quantity is required", nameof(orderDetails));
            if (!orderDetails.Commissions.HasValue)
                throw new ArgumentException("Commissions are required", nameof(orderDetails));

            // Create new position record
            var position = new TradeRecords
            {
                position_uuid = Guid.NewGuid(),
                position_type = !string.IsNullOrEmpty(originalPositionType) 
                    ? originalPositionType.ToUpperInvariant() 
                    : (orderDetails.Trade_Type == "BUY" ? "LONG" : "SHORT"),
                asset_pair = orderDetails.Asset_Pair,
                acquired_price = orderDetails.Acquired_Price.Value,
                acquired_quantity = orderDetails.Acquired_Quantity.Value,
                total_commissions = orderDetails.Commissions.Value,
                profit_loss = 0m,
                percentage_return = 0m,
                leftover_quantity = orderDetails.Acquired_Quantity.Value,
                is_position_closed = false
            };

            // Create opening trade record
            var openingTrade = new OpeningTrades
            {
                trade_id = orderDetails.Trade_Id ?? throw new ArgumentException("Trade ID is required", nameof(orderDetails)),
                side = orderDetails.Trade_Type,
                position_uuid = position.position_uuid,
                asset_pair = orderDetails.Asset_Pair,
                acquired_quantity = orderDetails.Acquired_Quantity.Value,
                acquired_price = orderDetails.Acquired_Price.Value,
                commission = orderDetails.Commissions.Value,
                trade_time = orderDetails.Acquired_Time ?? DateTime.UtcNow
            };

            // Save both records
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.TradeRecords.Add(position);
                _context.OpeningTrades.Add(openingTrade);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return position;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<TradeRecords> UpdatePositionFromClosingOrderAsync(FinalizedOrderDetails orderDetails, Guid positionId)
        {
            if (string.IsNullOrEmpty(orderDetails.Trade_Type))
                throw new ArgumentException("Trade type is required", nameof(orderDetails));
            if (string.IsNullOrEmpty(orderDetails.Asset_Pair))
                throw new ArgumentException("Asset pair is required", nameof(orderDetails));
            if (!orderDetails.Acquired_Price.HasValue)
                throw new ArgumentException("Acquired price is required", nameof(orderDetails));
            if (!orderDetails.Acquired_Quantity.HasValue)
                throw new ArgumentException("Acquired quantity is required", nameof(orderDetails));
            if (!orderDetails.Commissions.HasValue)
                throw new ArgumentException("Commissions are required", nameof(orderDetails));

            var position = await _context.TradeRecords
                .FirstOrDefaultAsync(p => p.position_uuid == positionId);

            if (position == null)
                throw new InvalidOperationException($"Position {positionId} not found");

            var openingTrade = await _context.OpeningTrades
                .FirstOrDefaultAsync(t => t.position_uuid == positionId);

            if (openingTrade == null)
                throw new InvalidOperationException($"Opening trade for position {positionId} not found");

            // Create closing trade record
            var closingTrade = new ClosingTrades
            {
                trade_id = orderDetails.Trade_Id ?? throw new ArgumentException("Trade ID is required", nameof(orderDetails)),
                side = orderDetails.Trade_Type,
                position_uuid = positionId,
                opening_trade_id = openingTrade.trade_id,
                asset_pair = orderDetails.Asset_Pair,
                offloaded_quantity = orderDetails.Acquired_Quantity.Value,
                offloaded_price = orderDetails.Acquired_Price.Value,
                commission = orderDetails.Commissions.Value,
                trade_time = orderDetails.Acquired_Time ?? DateTime.UtcNow
            };

            // Update position
            position.leftover_quantity -= orderDetails.Acquired_Quantity.Value;
            position.total_commissions += orderDetails.Commissions.Value;
            
            // Calculate P&L and returns
            decimal tradePnL = _calculator.CalculateProfitLoss(
                position.position_type == "LONG",
                orderDetails.Acquired_Quantity.Value,
                position.acquired_price,
                orderDetails.Acquired_Price.Value);

            position.profit_loss += tradePnL;
            position.percentage_return = _calculator.CalculatePercentageReturn(
                position.position_type == "LONG",
                position.acquired_price,
                orderDetails.Acquired_Price.Value);
            
            position.is_position_closed = position.leftover_quantity <= 0m;

            // Save changes
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.ClosingTrades.Add(closingTrade);
                _context.TradeRecords.Update(position);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return position;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<TradeRecords>> GetOpenPositionsAsync(string? assetPair = null)
        {
            var query = _context.TradeRecords
                .Where(p => !p.is_position_closed && p.leftover_quantity > 0);

            if (!string.IsNullOrWhiteSpace(assetPair))
                query = query.Where(p => p.asset_pair == assetPair);

            return await query.ToListAsync();
        }

        public async Task<TradeRecords?> GetPositionByIdAsync(Guid positionId)
        {
            return await _context.TradeRecords
                .FirstOrDefaultAsync(p => p.position_uuid == positionId);
        }

        public async Task<TradeRecords> UpdatePositionFromClosingOrderAsync(FinalizedOrderDetails orderDetails)
        {
            if (string.IsNullOrEmpty(orderDetails.Trade_Type))
                throw new ArgumentException("Trade type is required", nameof(orderDetails));
            if (string.IsNullOrEmpty(orderDetails.Asset_Pair))
                throw new ArgumentException("Asset pair is required", nameof(orderDetails));
            if (!orderDetails.Acquired_Price.HasValue)
                throw new ArgumentException("Acquired price is required", nameof(orderDetails));
            if (!orderDetails.Acquired_Quantity.HasValue)
                throw new ArgumentException("Acquired quantity is required", nameof(orderDetails));
            if (!orderDetails.Commissions.HasValue)
                throw new ArgumentException("Commissions are required", nameof(orderDetails));

            // For SELL orders, we need to find LONG positions to close
            // For BUY orders, we need to find SHORT positions to close
            string expectedPositionType = orderDetails.Trade_Type == "SELL" ? "LONG" : "SHORT";
            
            decimal remainingQuantityToClose = orderDetails.Acquired_Quantity.Value;
            TradeRecords? lastUpdatedPosition = null;
            
            // Start a transaction for the entire cascading operation
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                while (remainingQuantityToClose > 0)
                {
                    // Find the earliest open position with leftover quantity > 0
                    var position = await _context.TradeRecords
                        .Where(p => p.asset_pair == orderDetails.Asset_Pair && 
                                   p.position_type == expectedPositionType &&
                                   !p.is_position_closed && 
                                   p.leftover_quantity > 0)
                        .OrderBy(p => p.record_opened_at) // Close oldest position first (FIFO)
                        .FirstOrDefaultAsync();

                    if (position == null)
                        throw new InvalidOperationException($"No open {expectedPositionType} position found for {orderDetails.Asset_Pair} to close. Remaining quantity to close: {remainingQuantityToClose}");

                    // Get the opening trade for this position
                    var openingTrade = await _context.OpeningTrades
                        .FirstOrDefaultAsync(t => t.position_uuid == position.position_uuid);

                    if (openingTrade == null)
                        throw new InvalidOperationException($"Opening trade for position {position.position_uuid} not found");

                    // Calculate how much we can close from this position
                    decimal quantityToCloseFromThisPosition = Math.Min(remainingQuantityToClose, position.leftover_quantity);
                    
                    // Create closing trade record for this partial closure
                    var closingTrade = new ClosingTrades
                    {
                        trade_id = $"{orderDetails.Trade_Id}_{position.position_uuid}", // Ensure unique trade ID
                        side = orderDetails.Trade_Type,
                        position_uuid = position.position_uuid,
                        opening_trade_id = openingTrade.trade_id,
                        asset_pair = orderDetails.Asset_Pair,
                        offloaded_quantity = quantityToCloseFromThisPosition,
                        offloaded_price = orderDetails.Acquired_Price.Value,
                        commission = orderDetails.Commissions.Value * (quantityToCloseFromThisPosition / orderDetails.Acquired_Quantity.Value), // Proportional commission
                        trade_time = orderDetails.Acquired_Time ?? DateTime.UtcNow
                    };

                    // Update position
                    position.leftover_quantity -= quantityToCloseFromThisPosition;
                    position.total_commissions += closingTrade.commission;
                    
                    // Calculate P&L for this partial closure
                    decimal tradePnL = _calculator.CalculateProfitLoss(
                        position.position_type == "LONG",
                        quantityToCloseFromThisPosition,
                        position.acquired_price,
                        orderDetails.Acquired_Price.Value);

                    position.profit_loss += tradePnL;
                    position.percentage_return = _calculator.CalculatePercentageReturn(
                        position.position_type == "LONG",
                        position.acquired_price,
                        orderDetails.Acquired_Price.Value);
                    
                    position.is_position_closed = position.leftover_quantity <= 0m;

                    // Save the closing trade and update the position
                    _context.ClosingTrades.Add(closingTrade);
                    _context.TradeRecords.Update(position);
                    await _context.SaveChangesAsync();

                    // Update tracking variables
                    remainingQuantityToClose -= quantityToCloseFromThisPosition;
                    lastUpdatedPosition = position;

                    // If we've closed the entire quantity, break out of the loop
                    if (remainingQuantityToClose <= 0)
                        break;
                }

                await transaction.CommitAsync();
                
                if (lastUpdatedPosition == null)
                    throw new InvalidOperationException("No positions were updated during the closing operation");
                
                return lastUpdatedPosition;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
} 