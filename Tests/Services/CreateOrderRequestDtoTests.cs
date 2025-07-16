using System.ComponentModel.DataAnnotations;
using crypto_bot_api.Models.DTOs.Orders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class CreateOrderRequestDtoTests
    {
        private CreateOrderRequestDto CreateValidOrderRequest()
        {
            return new CreateOrderRequestDto
            {
                ClientOrderId = "test-order-123",
                ProductId = "BTC-USD",
                Side = "BUY",
                PositionType = "LONG",
                OrderConfiguration = new OrderConfigurationDto
                {
                    LimitLimitGtc = new LimitLimitGtcDto
                    {
                        BaseSize = "0.001",
                        LimitPrice = "50000.00"
                    }
                }
            };
        }

        private void ValidateModel(object model, int expectedErrorCount = 0)
        {
            var validationResults = new List<ValidationResult>();
            var context = new ValidationContext(model);
            var isValid = Validator.TryValidateObject(model, context, validationResults, true);

            Assert.AreEqual(expectedErrorCount == 0, isValid);
            Assert.AreEqual(expectedErrorCount, validationResults.Count);
        }

        [TestMethod]
        public void CreateOrderRequestDto_ValidRequest_PassesValidation()
        {
            // Arrange
            var request = CreateValidOrderRequest();

            // Act & Assert
            ValidateModel(request);
        }

        [TestMethod]
        [DataRow("LONG")]
        [DataRow("SHORT")]
        [DataRow("long")]
        [DataRow("short")]
        [DataRow("Long")]
        [DataRow("Short")]
        public void PositionType_ValidValues_PassValidation(string positionType)
        {
            // Arrange
            var request = CreateValidOrderRequest();
            request.PositionType = positionType;

            // Act & Assert
            ValidateModel(request);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(null)]
        [DataRow("LONGG")]
        [DataRow("SHORTT")]
        [DataRow("MEDIUM")]
        [DataRow("LON")]
        [DataRow("SHO")]
        [DataRow("LONG ")]
        [DataRow(" LONG")]
        [DataRow("SHORT ")]
        [DataRow(" SHORT")]
        public void PositionType_InvalidValues_FailValidation(string positionType)
        {
            // Arrange
            var request = CreateValidOrderRequest();
            request.PositionType = positionType;

            // Act & Assert
            ValidateModel(request, expectedErrorCount: 1);
        }

        [TestMethod]
        public void GetNormalizedPositionType_ReturnsUpperCase()
        {
            // Arrange
            var request = CreateValidOrderRequest();
            
            // Test various cases
            var testCases = new Dictionary<string, string>
            {
                { "long", "LONG" },
                { "LONG", "LONG" },
                { "Long", "LONG" },
                { "short", "SHORT" },
                { "SHORT", "SHORT" },
                { "Short", "SHORT" }
            };

            foreach (var testCase in testCases)
            {
                // Arrange
                request.PositionType = testCase.Key;

                // Act
                var result = request.GetNormalizedPositionType();

                // Assert
                Assert.AreEqual(testCase.Value, result);
            }
        }

        [TestMethod]
        public void Side_ValidValues_PassValidation()
        {
            // Arrange
            var request = CreateValidOrderRequest();

            // Test both valid values
            foreach (var side in new[] { "BUY", "SELL" })
            {
                request.Side = side;
                ValidateModel(request);
            }
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(null)]
        [DataRow("BUYY")]
        [DataRow("SELLL")]
        [DataRow("Buy")]
        [DataRow("Sell")]
        [DataRow("buy")]
        [DataRow("sell")]
        public void Side_InvalidValues_FailValidation(string side)
        {
            // Arrange
            var request = CreateValidOrderRequest();
            request.Side = side;

            // Act & Assert
            ValidateModel(request, expectedErrorCount: 1);
        }
    }
} 