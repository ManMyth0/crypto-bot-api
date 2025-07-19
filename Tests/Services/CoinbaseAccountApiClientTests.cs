using Moq;
using System.Net;
using System.Text.Json.Nodes;
using crypto_bot_api.Services;
using crypto_bot_api.Tests.Utilities;
using crypto_bot_api.CustomExceptions;

namespace crypto_bot_api.Tests.Services
{
    [TestClass]
    public class CoinbaseAccountApiClientTests
    {
        private Mock<HttpMessageHandler> _mockHttpHandler = null!;
        private ICoinbaseAccountApiClient _accountClient = null!;

        private static readonly string SuccessfulAccountsResponse = @"{
            ""accounts"": [
                {
                    ""uuid"": ""12345678-1234-1234-1234-123456789012"",
                    ""name"": ""BTC Wallet"",
                    ""currency"": ""BTC"",
                    ""available_balance"": {
                        ""value"": ""0.1"",
                        ""currency"": ""BTC""
                    }
                },
                {
                    ""uuid"": ""87654321-4321-4321-4321-210987654321"",
                    ""name"": ""ETH Wallet"",
                    ""currency"": ""ETH"",
                    ""available_balance"": {
                        ""value"": ""0"",
                        ""currency"": ""ETH""
                    }
                }
            ]
        }";

        private static readonly string SuccessfulAccountDetailResponse = @"{
            ""account"": {
                ""uuid"": ""12345678-1234-1234-1234-123456789012"",
                ""name"": ""BTC Wallet"",
                ""currency"": ""BTC"",
                ""available_balance"": {
                    ""value"": ""0.1"",
                    ""currency"": ""BTC""
                },
                ""default"": true,
                ""active"": true,
                ""created_at"": ""2024-01-01T00:00:00Z"",
                ""updated_at"": ""2024-01-01T00:00:00Z""
            }
        }";

        private static readonly string AccountsErrorResponse = @"{
            ""error"": ""INVALID_REQUEST"",
            ""message"": ""Invalid request parameters"",
            ""error_details"": ""The provided account UUID is invalid""
        }";

        [TestInitialize]
        public void TestInitialize()
        {
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _accountClient = new TestAccountApiClient(_mockHttpHandler);
        }

        private void SetupHttpResponseForMethod(HttpStatusCode statusCode, string content, string urlContains, HttpMethod method)
        {
            ((TestAccountApiClient)_accountClient).SetupHttpResponseForMethod(statusCode, content, urlContains, method);
        }

        [TestMethod]
        public async Task GetAccounts_ReturnsAllAccountsWhenSuccessful()
        {
            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulAccountsResponse,
                "accounts",
                HttpMethod.Get);

            var result = await _accountClient.GetAccountsAsync();

            Assert.IsNotNull(result);
            var accounts = result["accounts"]?.AsArray();
            Assert.IsNotNull(accounts);
            Assert.AreEqual(2, accounts.Count);
            
            // First account assertions
            var firstAccount = accounts[0]?.AsObject();
            Assert.IsNotNull(firstAccount);
            Assert.AreEqual("12345678-1234-1234-1234-123456789012", firstAccount["uuid"]?.GetValue<string>());
            Assert.AreEqual("BTC Wallet", firstAccount["name"]?.GetValue<string>());
            Assert.AreEqual("0.1", firstAccount["available_balance"]?.AsObject()?["value"]?.GetValue<string>());
            
            // Second account assertions
            var secondAccount = accounts[1]?.AsObject();
            Assert.IsNotNull(secondAccount);
            Assert.AreEqual("87654321-4321-4321-4321-210987654321", secondAccount["uuid"]?.GetValue<string>());
            Assert.AreEqual("ETH Wallet", secondAccount["name"]?.GetValue<string>());
            Assert.AreEqual("0", secondAccount["available_balance"]?.AsObject()?["value"]?.GetValue<string>());
        }

        [TestMethod]
        public async Task GetAccountByUuid_ReturnsAccountDetailsWhenSuccessful()
        {
            var accountUuid = "12345678-1234-1234-1234-123456789012";
            var expectedStatusCode = HttpStatusCode.OK;

            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulAccountDetailResponse,
                accountUuid,
                HttpMethod.Get);

            var result = await _accountClient.GetAccountByUuidAsync(accountUuid);

            Assert.IsNotNull(result);
            var account = result["account"]?.AsObject();
            Assert.IsNotNull(account);
            Assert.AreEqual(accountUuid, account["uuid"]?.GetValue<string>());
            Assert.AreEqual("BTC Wallet", account["name"]?.GetValue<string>());
            Assert.AreEqual("0.1", account["available_balance"]?.AsObject()?["value"]?.GetValue<string>());
        }

        [TestMethod]
        public async Task GetAccountByUuid_ThrowsExceptionWhenApiReturnsError()
        {
            var accountUuid = "invalid-uuid";
            SetupHttpResponseForMethod(
                HttpStatusCode.BadRequest,
                AccountsErrorResponse,
                accountUuid,
                HttpMethod.Get);

            try
            {
                await _accountClient.GetAccountByUuidAsync(accountUuid);
                Assert.Fail("Expected CoinbaseApiException was not thrown");
            }
            catch (CoinbaseApiException ex)
            {
                // Error content is captured in the exception message
                Assert.IsTrue(ex.Message.Contains("INVALID_REQUEST"));
            }
        }

        private class TestAccountApiClient : TestApiClientBase, ICoinbaseAccountApiClient
        {
            public TestAccountApiClient(Mock<HttpMessageHandler> mockHandler) 
                : base(mockHandler)
            {
            }

            public async Task<JsonObject> GetAccountsAsync()
            {
                return await SendRequestAsync<JsonObject>(
                    HttpMethod.Get,
                    "/api/v3/brokerage/accounts");
            }

            public async Task<JsonObject> GetAccountByUuidAsync(string account_uuid)
            {
                return await SendRequestAsync<JsonObject>(
                    HttpMethod.Get,
                    $"/api/v3/brokerage/accounts/{account_uuid}");
            }

            public async Task<JsonObject> GetAccountDetailsAsync()
            {
                // This is a helper method that calls GetAccountsAsync and GetAccountByUuidAsync
                // Not directly tested in these unit tests
                return await Task.FromResult(JsonNode.Parse("{}") as JsonObject ?? new JsonObject());
            }

            public new void SetupHttpResponseForMethod(HttpStatusCode statusCode, string content, string urlContains, HttpMethod method)
            {
                base.SetupHttpResponseForMethod(statusCode, content, urlContains, method);
            }
        }
    }
}