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

            // Arrange, it doesn't matter what we put in here so long as there are valid columns
            var testRecord = new TradeRecords
            {
                TradeTime = DateTime.UtcNow,
                AcquiredPrice = 50000.00m,
                AcquiredQuantity = 1.5m,
                TradeType = "BUY"
            };

            try
            {
                // CREATE - Test Insert
                _context.TradeRecords.Add(testRecord);
                await _context.SaveChangesAsync();
                Assert.IsTrue(testRecord.TradeId > 0, "Record should have been assigned an ID");

                // READ - Test Retrieval
                var retrievedRecord = await _context.TradeRecords
                    .FirstOrDefaultAsync(r => r.TradeId == testRecord.TradeId);
                
                Assert.IsNotNull(retrievedRecord, "Should be able to retrieve the record");
                Assert.AreEqual(testRecord.AcquiredPrice, retrievedRecord.AcquiredPrice);
                Assert.AreEqual(testRecord.AcquiredQuantity, retrievedRecord.AcquiredQuantity);
                Assert.AreEqual(testRecord.TradeType, retrievedRecord.TradeType);

                // UPDATE - Test Update
                retrievedRecord.SoldPrice = 55000.00m;
                retrievedRecord.OffloadTime = DateTime.UtcNow;
                retrievedRecord.ProfitLoss = (55000.00m * 1.5m) - (50000.00m * 1.5m);
                retrievedRecord.PercentageOfReturn = ((55000.00m - 50000.00m) / 50000.00m) * 100;
                
                await _context.SaveChangesAsync();

                var updatedRecord = await _context.TradeRecords
                    .FirstOrDefaultAsync(r => r.TradeId == testRecord.TradeId);
                
                Assert.IsNotNull(updatedRecord, "Should be able to retrieve the updated record");
                Assert.AreEqual(55000.00m, updatedRecord.SoldPrice);
                Assert.IsNotNull(updatedRecord.OffloadTime);
                Assert.AreEqual(7500.00m, updatedRecord.ProfitLoss);
                Assert.AreEqual(10.00m, updatedRecord.PercentageOfReturn);

                // DELETE - Test Delete
                _context.TradeRecords.Remove(updatedRecord);
                await _context.SaveChangesAsync();

                var deletedRecord = await _context.TradeRecords
                    .FirstOrDefaultAsync(r => r.TradeId == testRecord.TradeId);
                
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