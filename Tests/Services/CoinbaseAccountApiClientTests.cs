using Moq;
using System.Net;
using System.Text;
using Moq.Protected;
using System.Text.Json;
using crypto_bot_api.Services;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;
using crypto_bot_api.Models.DTOs;
using Microsoft.Extensions.Configuration;

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
            _mockHttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == method &&
                        req.RequestUri != null &&
                        req.RequestUri.ToString().Contains(urlContains)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
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
            Assert.IsNotNull(result.Accounts);
            Assert.AreEqual(2, result.Accounts.Count);
            Assert.AreEqual(expectedStatusCode, HttpStatusCode.OK, "Expected status code 200 OK");
            
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

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Account);
            Assert.AreEqual(expectedStatusCode, HttpStatusCode.OK, "Expected status code 200 OK");
            Assert.AreEqual(accountUuid, result.Account.Uuid);
            Assert.AreEqual("BTC Wallet", result.Account.Name);
            Assert.AreEqual("0.1", result.Account.AvailableBalance?.Value);
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
                var errorResponse = JsonSerializer.Deserialize<ErrorResponseDto>(ex.Message);
                Assert.IsNotNull(errorResponse);
                Assert.AreEqual("INVALID_REQUEST", errorResponse.Error);
                Assert.AreEqual("Invalid request parameters", errorResponse.Message);
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
                var errorResponse = JsonSerializer.Deserialize<ErrorResponseDto>(ex.Message);
                Assert.IsNotNull(errorResponse);
                Assert.AreEqual("INVALID_REQUEST", errorResponse.Error);
                Assert.AreEqual("Invalid request parameters", errorResponse.Message);
            }
        }

        private class TestAccountApiClient : ICoinbaseAccountApiClient
        {
            private readonly HttpClient _client;
            private readonly Mock<HttpMessageHandler> _mockHandler;

            public TestAccountApiClient(Mock<HttpMessageHandler> mockHandler)
            {
                _mockHandler = mockHandler;
                _client = new HttpClient(mockHandler.Object)
                {
                    BaseAddress = new Uri("https://api.coinbase.com")
                };
            }

            public async Task<AccountsResponseDto> GetAccountsAsync()
            {
                string endpoint = "/api/v3/brokerage/accounts";
                string fullUrl = $"https://api.coinbase.com{endpoint}";

                var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                var response = await _client.SendAsync(request);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new CoinbaseApiException(jsonResponse);
                }

                return JsonSerializer.Deserialize<AccountsResponseDto>(
                    jsonResponse,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                    ?? new AccountsResponseDto();
            }

            public async Task<AccountDetailResponseDto> GetAccountByUuidAsync(string account_uuid)
            {
                string endpoint = $"/api/v3/brokerage/accounts/{account_uuid}";
                string fullUrl = $"https://api.coinbase.com{endpoint}";

                var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                var response = await _client.SendAsync(request);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new CoinbaseApiException(jsonResponse);
                }

                return JsonSerializer.Deserialize<AccountDetailResponseDto>(
                    jsonResponse,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new AccountDetailResponseDto();
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
        }
    }
}