using System.Text.Json.Nodes;
using crypto_bot_api.Services;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class AssembleOrderDetailsServiceTests
    {
        private IAssembleOrderDetailsService _assembleService = null!;
        private const string TestOrderId = "test-order-123";
        private static readonly string TestTradeTime = DateTime.UtcNow.ToString("O");

        [TestInitialize]
        public void TestInitialize()
        {
            _assembleService = new AssembleOrderDetailsService();
        }

        [TestMethod]
        public void AssembleFromFills_WithMultipleFills_ReturnsCorrectDetails()
        {
            // Arrange
            var fills = new JsonArray
            {
                new JsonObject
                {
                    ["trade_id"] = "trade-123",
                    ["side"] = "BUY",
                    ["product_id"] = "BTC-USD",
                    ["trade_time"] = TestTradeTime,
                    ["price"] = "50000.00",
                    ["size"] = "1.5",
                    ["commission"] = "75.00"
                },
                new JsonObject
                {
                    ["trade_id"] = "trade-124",
                    ["side"] = "BUY",
                    ["product_id"] = "BTC-USD",
                    ["trade_time"] = TestTradeTime,
                    ["price"] = "55000.00",
                    ["size"] = "0.5",
                    ["commission"] = "25.00"
                }
            };

            // Act
            var result = _assembleService.AssembleFromFills(TestOrderId, fills);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual("trade-123", result.Trade_Id);
            Assert.AreEqual("BUY", result.Trade_Type);
            Assert.AreEqual("BTC-USD", result.Asset_Pair);
            Assert.AreEqual(100.00m, result.Commissions);
            Assert.AreEqual(51250.00m, result.Acquired_Price);
            Assert.AreEqual(2.0m, result.Acquired_Quantity);
            Assert.AreEqual(DateTime.Parse(TestTradeTime), result.Acquired_Time);
        }

        [TestMethod]
        public void AssembleFromFills_WithSingleFill_ReturnsCorrectDetails()
        {
            // Arrange
            var fills = new JsonArray
            {
                new JsonObject
                {
                    ["trade_id"] = "trade-123",
                    ["side"] = "BUY",
                    ["product_id"] = "BTC-USD",
                    ["trade_time"] = TestTradeTime,
                    ["price"] = "50000.00",
                    ["size"] = "1.5",
                    ["commission"] = "75.00"
                }
            };

            // Act
            var result = _assembleService.AssembleFromFills(TestOrderId, fills);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual("trade-123", result.Trade_Id);
            Assert.AreEqual("BUY", result.Trade_Type);
            Assert.AreEqual("BTC-USD", result.Asset_Pair);
            Assert.AreEqual(75.00m, result.Commissions);
            Assert.AreEqual(50000.00m, result.Acquired_Price);
            Assert.AreEqual(1.5m, result.Acquired_Quantity);
            Assert.AreEqual(DateTime.Parse(TestTradeTime), result.Acquired_Time);
        }

        [TestMethod]
        public void AssembleFromFills_WithNoFills_ReturnsMinimalDetails()
        {
            // Arrange
            var fills = new JsonArray();

            // Act
            var result = _assembleService.AssembleFromFills(TestOrderId, fills);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual(string.Empty, result.Status);
            Assert.AreEqual(string.Empty, result.Trade_Id);
            Assert.AreEqual(string.Empty, result.Trade_Type);
            Assert.AreEqual(string.Empty, result.Asset_Pair);
            Assert.AreEqual(0m, result.Commissions);
        }

        [TestMethod]
        public void AssembleFromFills_WithTerminalStatus_UsesProvidedStatus()
        {
            // Arrange
            var fills = new JsonArray
            {
                new JsonObject
                {
                    ["trade_id"] = "trade-123",
                    ["side"] = "BUY",
                    ["product_id"] = "BTC-USD",
                    ["trade_time"] = TestTradeTime,
                    ["price"] = "50000.00",
                    ["size"] = "1.5",
                    ["commission"] = "75.00"
                }
            };

            // Act
            var result = _assembleService.AssembleFromFills(TestOrderId, fills, "CANCELLED");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("CANCELLED", result.Status);
            Assert.AreEqual("trade-123", result.Trade_Id);
            Assert.AreEqual("BUY", result.Trade_Type);
            Assert.AreEqual("BTC-USD", result.Asset_Pair);
            Assert.AreEqual(75.00m, result.Commissions);
            Assert.AreEqual(50000.00m, result.Acquired_Price);
            Assert.AreEqual(1.5m, result.Acquired_Quantity);
            Assert.AreEqual(DateTime.Parse(TestTradeTime), result.Acquired_Time);
        }

        [TestMethod]
        public void AssembleFromFills_WithNullFills_ReturnsMinimalDetails()
        {
            // Act
            var result = _assembleService.AssembleFromFills(TestOrderId, null);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual(string.Empty, result.Status);
            Assert.AreEqual(string.Empty, result.Trade_Id);
            Assert.AreEqual(string.Empty, result.Trade_Type);
            Assert.AreEqual(string.Empty, result.Asset_Pair);
            Assert.AreEqual(0m, result.Commissions);
        }

        [TestMethod]
        public void AssembleFromFills_WithInitialSize_ReturnsCorrectDetails()
        {
            // Arrange
            var fills = new JsonArray
            {
                new JsonObject
                {
                    ["trade_id"] = "trade-123",
                    ["side"] = "BUY",
                    ["product_id"] = "BTC-USD",
                    ["trade_time"] = TestTradeTime,
                    ["price"] = "50000.00",
                    ["size"] = "1.5",
                    ["commission"] = "75.00"
                }
            };
            decimal initialSize = 2.0m;

            // Act
            var result = _assembleService.AssembleFromFills(TestOrderId, fills, null, initialSize);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual("FILLED", result.Status);
            Assert.AreEqual(initialSize, result.Initial_Size);
            Assert.AreEqual(1.5m, result.Acquired_Quantity); // Actual filled amount
        }

        [TestMethod]
        public void AssembleFromFills_WithNoFillsButInitialSize_ReturnsInitialSize()
        {
            // Arrange
            var fills = new JsonArray();
            decimal initialSize = 2.0m;

            // Act
            var result = _assembleService.AssembleFromFills(TestOrderId, fills, null, initialSize);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestOrderId, result.Order_Id);
            Assert.AreEqual(string.Empty, result.Status);
            Assert.IsTrue(result.Initial_Size.HasValue);
            Assert.AreEqual(initialSize, result.Initial_Size.Value);
            Assert.AreEqual(0m, result.Acquired_Quantity);
        }
    }
} 