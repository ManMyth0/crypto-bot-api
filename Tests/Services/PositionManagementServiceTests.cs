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
            Assert.AreEqual("LONG", result.position_type);
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
                last_updated = DateTime.UtcNow
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
                last_updated = DateTime.UtcNow
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
    }
} 