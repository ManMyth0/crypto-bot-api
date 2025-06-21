using System;
using System.IO;
using crypto_bot_api.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                asset_pair = "BTC/USD",
                acquired_price = 50000.00m,
                acquired_quantity = 1.5m,
                leftover_quantity = 1.5m,
                total_commissions = 10.00m,
                is_position_closed = false,
                last_updated = DateTime.UtcNow
            };

            try
            {
                // CREATE - Test Insert
                _context.TradeRecords.Add(testRecord);
                await _context.SaveChangesAsync();

                // READ - Test Retrieval
                var retrievedRecord = await _context.TradeRecords
                    .FirstOrDefaultAsync(r => r.position_uuid == positionId);
                
                Assert.IsNotNull(retrievedRecord, "Should be able to retrieve the record");
                Assert.AreEqual(testRecord.acquired_price, retrievedRecord.acquired_price);
                Assert.AreEqual(testRecord.acquired_quantity, retrievedRecord.acquired_quantity);
                Assert.AreEqual(testRecord.asset_pair, retrievedRecord.asset_pair);
                Assert.AreEqual(testRecord.leftover_quantity, retrievedRecord.leftover_quantity);

                // UPDATE - Test Update
                retrievedRecord.leftover_quantity = 0.5m;
                retrievedRecord.profit_loss = (55000.00m * 1.0m) - (50000.00m * 1.0m) - 20.00m; // Sold 1.0 BTC
                retrievedRecord.percentage_return = ((55000.00m - 50000.00m) / 50000.00m) * 100;
                retrievedRecord.total_commissions = 20.00m; // Added sell commission
                retrievedRecord.last_updated = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                var updatedRecord = await _context.TradeRecords
                    .FirstOrDefaultAsync(r => r.position_uuid == positionId);
                
                Assert.IsNotNull(updatedRecord, "Should be able to retrieve the updated record");
                Assert.AreEqual(0.5m, updatedRecord.leftover_quantity);
                Assert.AreEqual(4980.00m, updatedRecord.profit_loss); // 5000 profit - 20 commission
                Assert.AreEqual(10.00m, updatedRecord.percentage_return);
                Assert.AreEqual(20.00m, updatedRecord.total_commissions);

                // DELETE - Test Delete
                _context.TradeRecords.Remove(updatedRecord);
                await _context.SaveChangesAsync();

                var deletedRecord = await _context.TradeRecords
                    .FirstOrDefaultAsync(r => r.position_uuid == positionId);
                
                Assert.IsNull(deletedRecord, "Record should have been deleted");
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