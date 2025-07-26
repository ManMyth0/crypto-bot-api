using crypto_bot_api.Data;
using Microsoft.EntityFrameworkCore;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class DatabaseConnectionTests
    {
        private AppDbContext? _context;
        private IConfiguration? _configuration;

        [TestInitialize]
        public void Setup()
        {
            // Build configuration to access user secrets
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets<DatabaseConnectionTests>();
            
            _configuration = builder.Build();

            // Get connection string from user secrets
            var connectionString = _configuration["PostgresLocalDatabaseConnection"];
            
            // Create DbContext options
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .EnableSensitiveDataLogging() // Helpful for debugging
                .Options;

            _context = new AppDbContext(options);
        }

        [TestMethod]
        public async Task DatabaseCRUD_FullLifecycle_WorksAsExpected()
        {
            // Verify context is initialized
            Assert.IsNotNull(_context, "Database context should be initialized");

            // Arrange
            var positionId = Guid.NewGuid();
            var testRecord = new TradeRecords
            {
                position_uuid = positionId,
                position_type = "LONG",
                asset_pair = "BTC/USD",
                acquired_price = 50000.00m,
                acquired_quantity = 1.5m,
                total_commissions = 10.00m,
                profit_loss = 0m,
                percentage_return = 0m,
                leftover_quantity = 1.0m,
                is_position_closed = false,
                record_opened_at = DateTime.UtcNow
            };

            var openingTrade = new OpeningTrades
            {
                trade_id = "BUY123",
                side = "BUY",
                position_uuid = positionId,
                asset_pair = "BTC/USD",
                acquired_quantity = 1.5m,
                acquired_price = 50000.00m,
                commission = 10.00m,
                trade_time = DateTime.UtcNow
            };

            try
            {
                // CREATE - Test Insert Trade Record
                _context.TradeRecords.Add(testRecord);
                await _context.SaveChangesAsync();

                // READ - Test Retrieval of Trade Record
                var retrievedRecord = await _context.TradeRecords
                    .FirstOrDefaultAsync(r => r.position_uuid == positionId);
                
                Assert.IsNotNull(retrievedRecord, "Should be able to retrieve the trade record");
                Assert.AreEqual(testRecord.position_type, retrievedRecord.position_type);
                Assert.AreEqual(testRecord.acquired_price, retrievedRecord.acquired_price);
                Assert.AreEqual(testRecord.acquired_quantity, retrievedRecord.acquired_quantity);
                Assert.AreEqual(testRecord.asset_pair, retrievedRecord.asset_pair);
                Assert.AreEqual(testRecord.leftover_quantity, retrievedRecord.leftover_quantity);

                // CREATE - Test Insert Opening Trade
                _context.OpeningTrades.Add(openingTrade);
                await _context.SaveChangesAsync();

                // READ - Test Retrieval of Opening Trade
                var retrievedOpeningTrade = await _context.OpeningTrades
                    .FirstOrDefaultAsync(t => t.trade_id == "BUY123");
                
                Assert.IsNotNull(retrievedOpeningTrade, "Should be able to retrieve the opening trade");
                Assert.AreEqual(openingTrade.side, retrievedOpeningTrade.side);
                Assert.AreEqual(openingTrade.acquired_price, retrievedOpeningTrade.acquired_price);
                Assert.AreEqual(openingTrade.acquired_quantity, retrievedOpeningTrade.acquired_quantity);

                // CREATE - Test Insert Closing Trade
                var closingTrade = new ClosingTrades
                {
                    trade_id = "SELL123",
                    side = "SELL",
                    position_uuid = positionId,
                    opening_trade_id = "BUY123",
                    asset_pair = "BTC/USD",
                    offloaded_quantity = 1.0m,
                    offloaded_price = 55000.00m,
                    commission = 10.00m,
                    trade_time = DateTime.UtcNow
                };

                _context.ClosingTrades.Add(closingTrade);
                await _context.SaveChangesAsync();

                // READ - Test Retrieval of Closing Trade
                var retrievedClosingTrade = await _context.ClosingTrades
                    .FirstOrDefaultAsync(t => t.trade_id == "SELL123");
                
                Assert.IsNotNull(retrievedClosingTrade, "Should be able to retrieve the closing trade");
                Assert.AreEqual(closingTrade.side, retrievedClosingTrade.side);
                Assert.AreEqual(closingTrade.offloaded_price, retrievedClosingTrade.offloaded_price);
                Assert.AreEqual(closingTrade.offloaded_quantity, retrievedClosingTrade.offloaded_quantity);

                // UPDATE - Test Update Trade Record
                retrievedRecord.leftover_quantity = 0.5m;
                retrievedRecord.profit_loss = (55000.00m * 1.0m) - (50000.00m * 1.0m) - 20.00m; // Sold 1.0 BTC
                retrievedRecord.percentage_return = ((55000.00m - 50000.00m) / 50000.00m) * 100;
                retrievedRecord.total_commissions = 20.00m; // Added sell commission
                
                await _context.SaveChangesAsync();

                var updatedRecord = await _context.TradeRecords
                    .FirstOrDefaultAsync(r => r.position_uuid == positionId);
                
                Assert.IsNotNull(updatedRecord, "Should be able to retrieve the updated record");
                Assert.AreEqual(0.5m, updatedRecord.leftover_quantity);
                Assert.AreEqual(4980.00m, updatedRecord.profit_loss); // 5000 profit - 20 commission
                Assert.AreEqual(10.00m, updatedRecord.percentage_return);
                Assert.AreEqual(20.00m, updatedRecord.total_commissions);

                // DELETE - Test Delete (in correct order due to foreign key constraints)
                _context.ClosingTrades.Remove(retrievedClosingTrade);
                await _context.SaveChangesAsync();

                _context.OpeningTrades.Remove(retrievedOpeningTrade);
                await _context.SaveChangesAsync();

                _context.TradeRecords.Remove(updatedRecord);
                await _context.SaveChangesAsync();

                var deletedRecord = await _context.TradeRecords
                    .FirstOrDefaultAsync(r => r.position_uuid == positionId);
                Assert.IsNull(deletedRecord, "Trade record should have been deleted");

                var deletedOpeningTrade = await _context.OpeningTrades
                    .FirstOrDefaultAsync(t => t.trade_id == "BUY123");
                Assert.IsNull(deletedOpeningTrade, "Opening trade should have been deleted");

                var deletedClosingTrade = await _context.ClosingTrades
                    .FirstOrDefaultAsync(t => t.trade_id == "SELL123");
                Assert.IsNull(deletedClosingTrade, "Closing trade should have been deleted");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Database operation failed: {ex.Message}");
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_context != null)
            {
                _context.Dispose();
                _context = null;
            }
        }
    }
} 