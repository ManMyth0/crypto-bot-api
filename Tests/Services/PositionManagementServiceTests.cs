using crypto_bot_api.Data;
using Microsoft.Data.Sqlite;
using crypto_bot_api.Models;
using crypto_bot_api.Services;
using Microsoft.EntityFrameworkCore;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class PositionManagementServiceTests
    {
        private SqliteConnection _connection = null!;
        private AppDbContext _dbContext = null!;
        private IPositionManagementService _positionManager = null!;
        private TradeMetricsCalculator _calculator = null!;
        private static readonly string TestTradeTime = DateTime.UtcNow.ToString("O");

        [TestInitialize]
        public void TestInitialize()
        {
            // Create and open a connection. This creates the SQLite in-memory database, which will persist until the connection is closed
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection) // Use SQLite with the in-memory connection
                .Options;

            _dbContext = new AppDbContext(options);
            _dbContext.Database.EnsureCreated(); // Create the schema in the database

            _calculator = new TradeMetricsCalculator();
            _positionManager = new PositionManagementService(_dbContext, _calculator);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _dbContext.Dispose();
            _connection.Dispose(); // This will delete the in-memory database
        }

        [TestMethod]
        public async Task CreatePositionFromOrderAsync_WithValidOrder_CreatesPosition()
        {
            // Arrange
            var orderDetails = new FinalizedOrderDetails
            {
                Trade_Id = "trade-123",
                Trade_Type = "BUY",
                Asset_Pair = "BTC-USD",
                Acquired_Time = DateTime.Parse(TestTradeTime),
                Acquired_Price = 50000.00m,
                Acquired_Quantity = 1.5m,
                Commissions = 75.00m
            };

            // Act
            var result = await _positionManager.CreatePositionFromOrderAsync(orderDetails);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("BUY", result.position_type);
            Assert.AreEqual("BTC-USD", result.asset_pair);
            Assert.AreEqual(50000.00m, result.acquired_price);
            Assert.AreEqual(1.5m, result.acquired_quantity);
            Assert.AreEqual(75.00m, result.total_commissions);
            Assert.AreEqual(0m, result.profit_loss);
            Assert.AreEqual(0m, result.percentage_return);
            Assert.AreEqual(1.5m, result.leftover_quantity);
            Assert.IsFalse(result.is_position_closed);

            // Verify opening trade was created
            var openingTrade = await _dbContext.OpeningTrades.FirstOrDefaultAsync(t => t.trade_id == "trade-123");
            Assert.IsNotNull(openingTrade);
            Assert.AreEqual(result.position_uuid, openingTrade.position_uuid);
            Assert.AreEqual("BUY", openingTrade.side);
            Assert.AreEqual("BTC-USD", openingTrade.asset_pair);
            Assert.AreEqual(1.5m, openingTrade.acquired_quantity);
            Assert.AreEqual(50000.00m, openingTrade.acquired_price);
            Assert.AreEqual(75.00m, openingTrade.commission);
        }

        [TestMethod]
        public async Task CreatePositionFromOrderAsync_WithSHORTPositionType_CreatesSHORTPosition()
        {
            // Arrange
            var orderDetails = new FinalizedOrderDetails
            {
                Trade_Id = "trade-124",
                Trade_Type = "SELL",
                Asset_Pair = "BTC-USD",
                Acquired_Time = DateTime.Parse(TestTradeTime),
                Acquired_Price = 50000.00m,
                Acquired_Quantity = 1.5m,
                Commissions = 75.00m
            };

            // Act
            var result = await _positionManager.CreatePositionFromOrderAsync(orderDetails, "SHORT");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("SHORT", result.position_type);
            Assert.AreEqual("BTC-USD", result.asset_pair);
            Assert.AreEqual(50000.00m, result.acquired_price);
            Assert.AreEqual(1.5m, result.acquired_quantity);
            Assert.AreEqual(75.00m, result.total_commissions);
            Assert.AreEqual(0m, result.profit_loss);
            Assert.AreEqual(0m, result.percentage_return);
            Assert.AreEqual(1.5m, result.leftover_quantity);
            Assert.IsFalse(result.is_position_closed);

            // Verify opening trade was created
            var openingTrade = await _dbContext.OpeningTrades.FirstOrDefaultAsync(t => t.trade_id == "trade-124");
            Assert.IsNotNull(openingTrade);
            Assert.AreEqual(result.position_uuid, openingTrade.position_uuid);
            Assert.AreEqual("SHORT", openingTrade.side);
            Assert.AreEqual("BTC-USD", openingTrade.asset_pair);
            Assert.AreEqual(1.5m, openingTrade.acquired_quantity);
            Assert.AreEqual(50000.00m, openingTrade.acquired_price);
            Assert.AreEqual(75.00m, openingTrade.commission);
        }

        [TestMethod]
        public async Task UpdatePositionFromClosingOrderAsync_WithValidOrder_UpdatesPosition()
        {
            // Arrange
            var position = new TradeRecords
            {
                position_uuid = Guid.NewGuid(),
                position_type = "LONG",
                asset_pair = "BTC-USD",
                acquired_price = 50000.00m,
                acquired_quantity = 1.5m,
                total_commissions = 75.00m,
                profit_loss = 0m,
                percentage_return = 0m,
                leftover_quantity = 1.5m,
                is_position_closed = false,
                record_opened_at = DateTime.UtcNow
            };

            var openingTrade = new OpeningTrades
            {
                trade_id = "trade-123",
                side = "BUY",
                position_uuid = position.position_uuid,
                asset_pair = "BTC-USD",
                acquired_quantity = 1.5m,
                acquired_price = 50000.00m,
                commission = 75.00m,
                trade_time = DateTime.Parse(TestTradeTime)
            };

            await _dbContext.TradeRecords.AddAsync(position);
            await _dbContext.OpeningTrades.AddAsync(openingTrade);
            await _dbContext.SaveChangesAsync();

            var orderDetails = new FinalizedOrderDetails
            {
                Trade_Id = "trade-124",
                Trade_Type = "SELL",
                Asset_Pair = "BTC-USD",
                Acquired_Time = DateTime.Parse(TestTradeTime),
                Acquired_Price = 55000.00m,
                Acquired_Quantity = 1.0m,
                Commissions = 50.00m
            };

            // Act
            var result = await _positionManager.UpdatePositionFromClosingOrderAsync(orderDetails, position.position_uuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0.5m, result.leftover_quantity); // 1.5 - 1.0
            Assert.AreEqual(125.00m, result.total_commissions); // 75 + 50
            Assert.IsFalse(result.is_position_closed); // Still has 0.5 quantity left

            // Verify P&L calculation
            var expectedPnL = (55000.00m - 50000.00m) * 1.0m; // (exit - entry) * quantity
            Assert.AreEqual(expectedPnL, result.profit_loss);

            // Verify closing trade was created
            var closingTrade = await _dbContext.ClosingTrades.FirstOrDefaultAsync(t => t.trade_id == "trade-124");
            Assert.IsNotNull(closingTrade);
            Assert.AreEqual(position.position_uuid, closingTrade.position_uuid);
            Assert.AreEqual("trade-123", closingTrade.opening_trade_id);
            Assert.AreEqual("SELL", closingTrade.side);
            Assert.AreEqual(1.0m, closingTrade.offloaded_quantity);
            Assert.AreEqual(55000.00m, closingTrade.offloaded_price);
            Assert.AreEqual(50.00m, closingTrade.commission);
        }

        [TestMethod]
        public async Task UpdatePositionFromClosingOrderAsync_WithFullClose_ClosesPosition()
        {
            // Arrange
            var position = new TradeRecords
            {
                position_uuid = Guid.NewGuid(),
                position_type = "LONG",
                asset_pair = "BTC-USD",
                acquired_price = 50000.00m,
                acquired_quantity = 1.5m,
                total_commissions = 75.00m,
                profit_loss = 0m,
                percentage_return = 0m,
                leftover_quantity = 1.5m,
                is_position_closed = false,
                record_opened_at = DateTime.UtcNow
            };

            var openingTrade = new OpeningTrades
            {
                trade_id = "trade-123",
                side = "BUY",
                position_uuid = position.position_uuid,
                asset_pair = "BTC-USD",
                acquired_quantity = 1.5m,
                acquired_price = 50000.00m,
                commission = 75.00m,
                trade_time = DateTime.Parse(TestTradeTime)
            };

            await _dbContext.TradeRecords.AddAsync(position);
            await _dbContext.OpeningTrades.AddAsync(openingTrade);
            await _dbContext.SaveChangesAsync();

            var orderDetails = new FinalizedOrderDetails
            {
                Trade_Id = "trade-124",
                Trade_Type = "SELL",
                Asset_Pair = "BTC-USD",
                Acquired_Time = DateTime.Parse(TestTradeTime),
                Acquired_Price = 55000.00m,
                Acquired_Quantity = 1.5m, // Full position size
                Commissions = 50.00m
            };

            // Act
            var result = await _positionManager.UpdatePositionFromClosingOrderAsync(orderDetails, position.position_uuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0m, result.leftover_quantity);
            Assert.IsTrue(result.is_position_closed);
        }

        [TestMethod]
        public async Task UpdatePositionFromClosingOrderAsync_CascadingLogic_ClosesMultiplePositions()
        {
            // Arrange - Create multiple positions with explicit 1-second timestamp differences
            var baseTime = DateTime.UtcNow;
            var position1 = new TradeRecords
            {
                position_uuid = Guid.NewGuid(),
                position_type = "LONG",
                asset_pair = "BTC-USD",
                acquired_price = 50000m,
                acquired_quantity = 2.0m,
                total_commissions = 10m,
                profit_loss = 0m,
                percentage_return = 0m,
                leftover_quantity = 5.0m, // Position 1 has 5.0 units
                is_position_closed = false,
                record_opened_at = baseTime.AddSeconds(-2) // Oldest position (2 seconds ago)
            };

            var position2 = new TradeRecords
            {
                position_uuid = Guid.NewGuid(),
                position_type = "LONG",
                asset_pair = "BTC-USD",
                acquired_price = 52000m,
                acquired_quantity = 1.0m,
                total_commissions = 5m,
                profit_loss = 0m,
                percentage_return = 0m,
                leftover_quantity = 3.0m, // Position 2 has 3.0 units
                is_position_closed = false,
                record_opened_at = baseTime.AddSeconds(-1) // Newer position (1 second ago)
            };

            var openingTrade1 = new OpeningTrades
            {
                trade_id = "BUY1",
                side = "BUY",
                position_uuid = position1.position_uuid,
                asset_pair = "BTC-USD",
                acquired_quantity = 2.0m,
                acquired_price = 50000m,
                commission = 10m,
                trade_time = baseTime.AddSeconds(-2)
            };

            var openingTrade2 = new OpeningTrades
            {
                trade_id = "BUY2",
                side = "BUY",
                position_uuid = position2.position_uuid,
                asset_pair = "BTC-USD",
                acquired_quantity = 1.0m,
                acquired_price = 52000m,
                commission = 5m,
                trade_time = baseTime.AddSeconds(-1)
            };

            await _dbContext.TradeRecords.AddAsync(position1);
            await _dbContext.TradeRecords.AddAsync(position2);
            await _dbContext.OpeningTrades.AddAsync(openingTrade1);
            await _dbContext.OpeningTrades.AddAsync(openingTrade2);
            await _dbContext.SaveChangesAsync();

            // Act - Try to sell 7.0 BTC (should close position1 completely and partially close position2)
            var sellOrder = new FinalizedOrderDetails
            {
                Trade_Id = "SELL123",
                Trade_Type = "SELL",
                Asset_Pair = "BTC-USD",
                Acquired_Price = 55000m,
                Acquired_Quantity = 7.0m, // Sell 7.0 units total
                Commissions = 20m,
                Acquired_Time = DateTime.UtcNow
            };

            var result = await _positionManager.UpdatePositionFromClosingOrderAsync(sellOrder, "LONG");

            // Assert
            Assert.IsNotNull(result);
            
            // Verify both positions were updated
            var updatedPosition1 = await _dbContext.TradeRecords.FindAsync(position1.position_uuid);
            var updatedPosition2 = await _dbContext.TradeRecords.FindAsync(position2.position_uuid);
            
            Assert.IsNotNull(updatedPosition1);
            Assert.IsNotNull(updatedPosition2);
            
            // FIFO Verification: Position 1 (oldest) should be fully closed first
            // Position 1: 5.0 - 5.0 = 0 (fully closed)
            Assert.AreEqual(0m, updatedPosition1.leftover_quantity, "Position 1 (oldest) should be fully closed");
            Assert.IsTrue(updatedPosition1.is_position_closed, "Position 1 should be marked as closed");
            
            // Position 2 (newer) should be partially closed
            // Position 2: 3.0 - 2.0 = 1.0 (partially closed)
            Assert.AreEqual(1.0m, updatedPosition2.leftover_quantity, "Position 2 should have 1.0 leftover");
            Assert.IsFalse(updatedPosition2.is_position_closed, "Position 2 should not be marked as closed");
            
            // Verify closing trades were created in FIFO order
            var closingTrades = await _dbContext.ClosingTrades
                .Where(ct => ct.trade_id.StartsWith("SELL123"))
                .OrderBy(ct => ct.trade_time) // Order by trade time to verify FIFO
                .ToListAsync();
            
            Assert.AreEqual(2, closingTrades.Count, "Should have created 2 closing trades");
            
            // Verify the first closing trade closed 5.0 from position 1 (oldest)
            var closingTrade1 = closingTrades.First(ct => ct.position_uuid == position1.position_uuid);
            Assert.AreEqual(5.0m, closingTrade1.offloaded_quantity, "First trade should close 5.0 from position 1");
            
            // Verify the second closing trade closed 2.0 from position 2 (newer)
            var closingTrade2 = closingTrades.First(ct => ct.position_uuid == position2.position_uuid);
            Assert.AreEqual(2.0m, closingTrade2.offloaded_quantity, "Second trade should close 2.0 from position 2");
            
            // Verify FIFO ordering by checking trade times
            Assert.IsTrue(closingTrade1.trade_time <= closingTrade2.trade_time, "First trade should be executed before second trade");
            
            // Verify proportional commission distribution
            var totalCommission = closingTrade1.commission + closingTrade2.commission;
            Assert.AreEqual(20m, totalCommission, 0.01m, "Total commission should equal original commission");
            
            // Verify commission is proportional to quantity closed
            var expectedCommission1 = 20m * (5.0m / 7.0m); // 5/7 of total commission
            var expectedCommission2 = 20m * (2.0m / 7.0m); // 2/7 of total commission
            Assert.AreEqual(expectedCommission1, closingTrade1.commission, 0.01m, "Commission should be proportional to quantity closed");
            Assert.AreEqual(expectedCommission2, closingTrade2.commission, 0.01m, "Commission should be proportional to quantity closed");
            
            // Verify total_commissions updates on positions
            Assert.AreEqual(10m + expectedCommission1, updatedPosition1.total_commissions, 0.01m, "Position 1 total_commissions should be updated");
            Assert.AreEqual(5m + expectedCommission2, updatedPosition2.total_commissions, 0.01m, "Position 2 total_commissions should be updated");
            
            // Verify profit_loss calculations
            var expectedPnL1 = (55000m - 50000m) * 5.0m; // (exit - entry) * quantity for position 1
            var expectedPnL2 = (55000m - 52000m) * 2.0m; // (exit - entry) * quantity for position 2
            Assert.AreEqual(expectedPnL1, updatedPosition1.profit_loss, 0.01m, "Position 1 profit_loss should be calculated correctly");
            Assert.AreEqual(expectedPnL2, updatedPosition2.profit_loss, 0.01m, "Position 2 profit_loss should be calculated correctly");
            
            // Verify percentage_return calculations
            var expectedReturn1 = ((55000m - 50000m) / 50000m) * 100m; // 10% return for position 1
            var expectedReturn2 = ((55000m - 52000m) / 52000m) * 100m; // ~5.77% return for position 2
            Assert.AreEqual(expectedReturn1, updatedPosition1.percentage_return, 0.01m, "Position 1 percentage_return should be calculated correctly");
            Assert.AreEqual(expectedReturn2, updatedPosition2.percentage_return, 0.01m, "Position 2 percentage_return should be calculated correctly");
        }

        [TestMethod]
        public async Task GetOpenPositionsAsync_WithFilter_ReturnsMatchingPositions()
        {
            // Arrange
            var positions = new[]
            {
                new TradeRecords
                {
                    position_uuid = Guid.NewGuid(),
                    position_type = "LONG",
                    asset_pair = "BTC-USD",
                    acquired_price = 50000.00m,
                    acquired_quantity = 1.5m,
                    leftover_quantity = 1.5m,
                    is_position_closed = false
                },
                new TradeRecords
                {
                    position_uuid = Guid.NewGuid(),
                    position_type = "LONG",
                    asset_pair = "ETH-USD",
                    acquired_price = 2000.00m,
                    acquired_quantity = 10m,
                    leftover_quantity = 10m,
                    is_position_closed = false
                },
                new TradeRecords
                {
                    position_uuid = Guid.NewGuid(),
                    position_type = "LONG",
                    asset_pair = "BTC-USD",
                    acquired_price = 45000.00m,
                    acquired_quantity = 2.0m,
                    leftover_quantity = 0m,
                    is_position_closed = true
                }
            };

            await _dbContext.TradeRecords.AddRangeAsync(positions);
            await _dbContext.SaveChangesAsync();

            // Act
            var btcPositions = await _positionManager.GetOpenPositionsAsync("BTC-USD");
            var allPositions = await _positionManager.GetOpenPositionsAsync();

            // Assert
            Assert.AreEqual(1, btcPositions.Count());
            Assert.AreEqual(2, allPositions.Count());
            Assert.IsTrue(btcPositions.All(p => p.asset_pair == "BTC-USD" && !p.is_position_closed));
            Assert.IsTrue(allPositions.All(p => !p.is_position_closed));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task CreatePositionFromOrderAsync_WithInvalidOrder_ThrowsException()
        {
            // Arrange
            var orderDetails = new FinalizedOrderDetails
            {
                // Missing required fields
                Trade_Id = "trade-123"
            };

            // Act
            await _positionManager.CreatePositionFromOrderAsync(orderDetails);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task UpdatePositionFromClosingOrderAsync_WithInvalidPositionId_ThrowsException()
        {
            // Arrange
            var orderDetails = new FinalizedOrderDetails
            {
                Trade_Id = "trade-124",
                Trade_Type = "SELL",
                Asset_Pair = "BTC-USD",
                Acquired_Price = 55000.00m,
                Acquired_Quantity = 1.0m,
                Commissions = 50.00m
            };

            // Act
            await _positionManager.UpdatePositionFromClosingOrderAsync(orderDetails, Guid.NewGuid());
        }

        [TestMethod]
        public async Task UpdatePositionFromClosingOrderAsync_WithOFFLOADPositionType_ClosesExistingPositions()
        {
            // Arrange
            var baseTime = DateTime.UtcNow;
            
            // Create a LONG position to be closed
            var position = new TradeRecords
            {
                position_uuid = Guid.NewGuid(),
                position_type = "LONG",
                asset_pair = "BTC-USD",
                acquired_price = 50000.00m,
                acquired_quantity = 2.0m,
                total_commissions = 50.00m,
                profit_loss = 0m,
                percentage_return = 0m,
                leftover_quantity = 2.0m,
                is_position_closed = false,
                record_opened_at = baseTime.AddSeconds(-1)
            };

            var openingTrade = new OpeningTrades
            {
                trade_id = "trade-1",
                side = "BUY",
                position_uuid = position.position_uuid,
                asset_pair = "BTC-USD",
                acquired_quantity = 2.0m,
                acquired_price = 50000.00m,
                commission = 50.00m,
                trade_time = baseTime.AddSeconds(-1)
            };

            await _dbContext.TradeRecords.AddAsync(position);
            await _dbContext.OpeningTrades.AddAsync(openingTrade);
            await _dbContext.SaveChangesAsync();

            // SELL order with OFFLOAD to close the position
            var orderDetails = new FinalizedOrderDetails
            {
                Trade_Id = "trade-sell-offload",
                Trade_Type = "SELL",
                Asset_Pair = "BTC-USD",
                Acquired_Time = baseTime,
                Acquired_Price = 55000.00m,
                Acquired_Quantity = 1.5m,
                Commissions = 30.00m
            };

            // Act
            var result = await _positionManager.UpdatePositionFromClosingOrderAsync(orderDetails, "LONG");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(position.position_uuid, result.position_uuid);

            // Verify position is partially closed
            var updatedPosition = await _dbContext.TradeRecords.FindAsync(position.position_uuid);
            Assert.IsNotNull(updatedPosition);
            Assert.AreEqual(0.5m, updatedPosition.leftover_quantity); // 2.0 - 1.5 = 0.5
            Assert.IsFalse(updatedPosition.is_position_closed);
            Assert.AreEqual(50.00m + 30.00m, updatedPosition.total_commissions);
            Assert.IsTrue(updatedPosition.profit_loss > 0); // Should have profit
            Assert.IsTrue(updatedPosition.percentage_return > 0); // Should have positive return

            // Verify closing trade was created
            var closingTrade = await _dbContext.ClosingTrades
                .FirstOrDefaultAsync(ct => ct.trade_id == "trade-sell-offload_" + position.position_uuid);
            Assert.IsNotNull(closingTrade);
            Assert.AreEqual(position.position_uuid, closingTrade.position_uuid);
            Assert.AreEqual("trade-1", closingTrade.opening_trade_id);
            Assert.AreEqual(1.5m, closingTrade.offloaded_quantity);
            Assert.AreEqual(55000.00m, closingTrade.offloaded_price);
            Assert.AreEqual(30.00m, closingTrade.commission);
        }
    }
} 