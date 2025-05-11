using System.Text.Json;
using crypto_bot_api.Helpers;
using crypto_bot_api.Models.DTOs;
using crypto_bot_api.CustomExceptions;
using Microsoft.Extensions.Configuration;

namespace crypto_bot_api.Services
{
    public class CoinbaseAccountApiClient : BaseCoinbaseApiClient, ICoinbaseAccountApiClient
    {
        private readonly new Ed25519JwtHelper _jwtHelper;

        public CoinbaseAccountApiClient(HttpClient client, IConfiguration config)
            : base(client, config)
        {
            // Create Ed25519 JWT helper
            _jwtHelper = new Ed25519JwtHelper(_apiKeyId, _apiSecret);
        }

        // Retrieves all Coinbase accounts
        public async Task<AccountsResponseDto> GetAccountsAsync()
        {
            string endpoint = "/api/v3/brokerage/accounts";
            string uri = $"GET {endpoint}";
            string fullUrl = $"{_baseUrl.TrimEnd('/')}{endpoint}";
            
            var jwt = _jwtHelper.GenerateJwt(uri);

            string jsonResponse = await SendAuthenticatedGetRequestAsync(jwt, fullUrl, "Failed to retrieve accounts.");
            return JsonSerializer.Deserialize<AccountsResponseDto>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                ?? new AccountsResponseDto();
        }

        // Retrieves details for a specific account.
        public async Task<AccountDetailResponseDto> GetAccountByUuidAsync(string account_uuid)
        {
            string endpoint = $"/api/v3/brokerage/accounts/{account_uuid}";
            string uri = $"GET {endpoint}";
            string fullUrl = $"{_baseUrl.TrimEnd('/')}{endpoint}";
        
            var jwt = _jwtHelper.GenerateJwt(uri);

            string jsonResponse = await SendAuthenticatedGetRequestAsync(jwt, fullUrl, $"Failed to retrieve account details for {account_uuid}.");
            return JsonSerializer.Deserialize<AccountDetailResponseDto>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new AccountDetailResponseDto();
        }

        public async Task<AccountDetailResponseDto?> GetAccountDetailsAsync()
        {
            // Get all accounts
            var accountsResponse = await GetAccountsAsync();
            
            if (accountsResponse.Accounts != null && accountsResponse.Accounts.Count > 0)
            {
                // Iterate through accounts to find one with balance > 0
                foreach (var account in accountsResponse.Accounts)
                {
                    if (account.AvailableBalance != null && 
                        !string.IsNullOrEmpty(account.AvailableBalance.Value) &&
                        decimal.TryParse(account.AvailableBalance.Value, out decimal balanceAmount) && 
                        balanceAmount > 0)
                    {
                        // Found an account with balance > 0, get its details
                        if (!string.IsNullOrEmpty(account.Uuid))
                        {
                            return await GetAccountByUuidAsync(account.Uuid);
                        }
                    }
                }
            }            
            // If we couldn't find any suitable account throw new explicit exception
            throw new CoinbaseApiException("No account with a positive balance could be found.");
        }
    }
} 