using System.Net;
using System.Text.Json;
using crypto_bot_api.Models.DTOs;
using crypto_bot_api.Models.DTOs.Orders;

namespace crypto_bot_api.Tests.Utilities
{
    public static class CoinbaseApiTestAssertions
    {
        public static void AssertSuccessfulResponse<T>(T result, HttpStatusCode expectedStatusCode)
        {
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedStatusCode, HttpStatusCode.OK, "Expected status code 200 OK");
        }

        public static void AssertAccountDetails(AccountDetailResponseDto result, string expectedUuid, string expectedName, string expectedBalance)
        {
            Assert.IsNotNull(result.Account);
            Assert.AreEqual(expectedUuid, result.Account.Uuid);
            Assert.AreEqual(expectedName, result.Account.Name);
            Assert.AreEqual(expectedBalance, result.Account.AvailableBalance?.Value);
        }

        public static void AssertErrorResponse(string errorMessage, string expectedError, string expectedMessage)
        {
            var errorResponse = JsonSerializer.Deserialize<ErrorResponseDto>(errorMessage);
            Assert.IsNotNull(errorResponse);
            Assert.AreEqual(expectedError, errorResponse.Error);
            Assert.AreEqual(expectedMessage, errorResponse.Message);
        }

        public static void AssertOrderResponse(CreateOrderResponseDto result, string expectedProductId, string expectedSide, string expectedClientOrderId)
        {
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.SuccessResponse);
            Assert.AreEqual(expectedProductId, result.SuccessResponse.ProductId);
            Assert.AreEqual(expectedSide, result.SuccessResponse.Side);
            Assert.AreEqual(expectedClientOrderId, result.SuccessResponse.ClientOrderId);
        }

        public static void AssertOrderErrorResponse(CreateOrderResponseDto result, string expectedError, string expectedMessage, string expectedErrorDetails)
        {
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.ErrorResponse);
            Assert.AreEqual(expectedError, result.ErrorResponse.Error);
            Assert.AreEqual(expectedMessage, result.ErrorResponse.Message);
            Assert.AreEqual(expectedErrorDetails, result.ErrorResponse.ErrorDetails);
        }
    }
} 