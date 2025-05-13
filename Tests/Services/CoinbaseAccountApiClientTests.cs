using Moq;
using System.Net;
using crypto_bot_api.Services;
using crypto_bot_api.Models.DTOs;
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

            CoinbaseApiTestAssertions.AssertSuccessfulResponse(result, expectedStatusCode);
            Assert.IsNotNull(result.Accounts);
            Assert.AreEqual(2, result.Accounts.Count);
            
            // First account assertions
            Assert.AreEqual("12345678-1234-1234-1234-123456789012", result.Accounts[0].Uuid);
            Assert.AreEqual("BTC Wallet", result.Accounts[0].Name);
            Assert.AreEqual("0.1", result.Accounts[0].AvailableBalance?.Value);
            
            // Second account assertions
            Assert.AreEqual("87654321-4321-4321-4321-210987654321", result.Accounts[1].Uuid);
            Assert.AreEqual("ETH Wallet", result.Accounts[1].Name);
            Assert.AreEqual("0", result.Accounts[1].AvailableBalance?.Value);
        }

        [TestMethod]
        public async Task GetAccountByUuid_ReturnsAccountDetailsWhenSuccessful()
        {
            string accountUuid = "12345678-1234-1234-1234-123456789012";
            var expectedStatusCode = HttpStatusCode.OK;
            SetupHttpResponseForMethod(
                expectedStatusCode,
                SuccessfulAccountDetailResponse,
                $"accounts/{accountUuid}",
                HttpMethod.Get);

            var result = await _accountClient.GetAccountByUuidAsync(accountUuid);

            CoinbaseApiTestAssertions.AssertSuccessfulResponse(result, expectedStatusCode);
            CoinbaseApiTestAssertions.AssertAccountDetails(result, accountUuid, "BTC Wallet", "0.1");
        }

        [TestMethod]
        public async Task GetAccounts_ThrowsExceptionWhenApiReturnsError()
        {
            SetupHttpResponseForMethod(
                HttpStatusCode.BadRequest,
                AccountsErrorResponse,
                "accounts",
                HttpMethod.Get);

            try
            {
                await _accountClient.GetAccountsAsync();
                Assert.Fail("Expected CoinbaseApiException was not thrown");
            }
            catch (CoinbaseApiException ex)
            {
                CoinbaseApiTestAssertions.AssertErrorResponse(ex.Message, "INVALID_REQUEST", "Invalid request parameters");
            }
        }

        [TestMethod]
        public async Task GetAccountByUuid_ThrowsExceptionWhenApiReturnsError()
        {
            string accountUuid = "invalid-uuid";
            SetupHttpResponseForMethod(
                HttpStatusCode.BadRequest,
                AccountsErrorResponse,
                $"accounts/{accountUuid}",
                HttpMethod.Get);

            try
            {
                await _accountClient.GetAccountByUuidAsync(accountUuid);
                Assert.Fail("Expected CoinbaseApiException was not thrown");
            }
            catch (CoinbaseApiException ex)
            {
                CoinbaseApiTestAssertions.AssertErrorResponse(ex.Message, "INVALID_REQUEST", "Invalid request parameters");
            }
        }

        private class TestAccountApiClient : TestApiClientBase, ICoinbaseAccountApiClient
        {
            public TestAccountApiClient(Mock<HttpMessageHandler> mockHandler) 
                : base(mockHandler)
            {
            }

            public async Task<AccountsResponseDto> GetAccountsAsync()
            {
                return await SendRequestAsync<AccountsResponseDto>(
                    HttpMethod.Get,
                    "/api/v3/brokerage/accounts");
            }

            public async Task<AccountDetailResponseDto> GetAccountByUuidAsync(string account_uuid)
            {
                return await SendRequestAsync<AccountDetailResponseDto>(
                    HttpMethod.Get,
                    $"/api/v3/brokerage/accounts/{account_uuid}");
            }

            public async Task<AccountDetailResponseDto?> GetAccountDetailsAsync()
            {
                var accountsResponse = await GetAccountsAsync();
                
                if (accountsResponse.Accounts != null && accountsResponse.Accounts.Count > 0)
                {
                    foreach (var account in accountsResponse.Accounts)
                    {
                        if (account.AvailableBalance != null && 
                            !string.IsNullOrEmpty(account.AvailableBalance.Value) &&
                            decimal.TryParse(account.AvailableBalance.Value, out decimal balanceAmount) && 
                            balanceAmount > 0)
                        {
                            if (!string.IsNullOrEmpty(account.Uuid))
                            {
                                return await GetAccountByUuidAsync(account.Uuid);
                            }
                        }
                    }
                }            
                throw new CoinbaseApiException("No account with a positive balance could be found.");
            }

            public new void SetupHttpResponseForMethod(HttpStatusCode statusCode, string content, string urlContains, HttpMethod method)
            {
                base.SetupHttpResponseForMethod(statusCode, content, urlContains, method);
            }
        }
    }
}