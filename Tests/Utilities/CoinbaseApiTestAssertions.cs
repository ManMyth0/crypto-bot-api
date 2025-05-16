using System.Net;
using System.Text.Json;

namespace crypto_bot_api.Tests.Utilities
{
    public static class CoinbaseApiTestAssertions
    {
        public static void AssertSuccessfulResponse<T>(T result, HttpStatusCode expectedStatusCode)
        {
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedStatusCode, HttpStatusCode.OK, "Expected status code 200 OK");
        }

        public static void AssertErrorResponse(string errorMessage, string expectedError, string expectedMessage)
        {
            var errorResponse = JsonSerializer.Deserialize<JsonElement>(errorMessage);
            Assert.IsNotNull(errorResponse);
            string error = errorResponse.GetProperty("error").GetString() ?? string.Empty;
            string message = errorResponse.GetProperty("message").GetString() ?? string.Empty;
            
            Assert.AreEqual(expectedError, error);
            Assert.AreEqual(expectedMessage, message);
        }
    }
} 