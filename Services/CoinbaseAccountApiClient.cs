using System.Text.Json;
using System.Text.Json.Nodes;
using crypto_bot_api.Helpers;
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
        public async Task<JsonObject> GetAccountsAsync()
        {
            string endpoint = "/api/v3/brokerage/accounts";
            string uri = $"GET {endpoint}";
            string fullUrl = $"{_baseUrl.TrimEnd('/')}{endpoint}";
            
            var jwt = _jwtHelper.GenerateJwt(uri);

            string jsonResponse = await SendAuthenticatedGetRequestAsync(jwt, fullUrl, "Failed to retrieve accounts.");
            return JsonSerializer.Deserialize<JsonObject>(jsonResponse) ?? new JsonObject();
        }

        // Retrieves details for a specific account.
        public async Task<JsonObject> GetAccountByUuidAsync(string account_uuid)
        {
            string endpoint = $"/api/v3/brokerage/accounts/{account_uuid}";
            string uri = $"GET {endpoint}";
            string fullUrl = $"{_baseUrl.TrimEnd('/')}{endpoint}";
        
            var jwt = _jwtHelper.GenerateJwt(uri);

            string jsonResponse = await SendAuthenticatedGetRequestAsync(jwt, fullUrl, $"Failed to retrieve account details for {account_uuid}.");
            return JsonSerializer.Deserialize<JsonObject>(jsonResponse) ?? new JsonObject();
        }

        public async Task<JsonObject> GetAccountDetailsAsync()
        {
            // Get all accounts
            var accountsResponse = await GetAccountsAsync();
            
            if (accountsResponse["accounts"] is JsonArray accountsArray && accountsArray.Count > 0)
            {
                // Iterate through accounts to find one with balance > 0
                foreach (var accountNode in accountsArray)
                {
                    if (accountNode is JsonObject account)
                    {
                        var availableBalance = account["available_balance"]?.AsObject();
                        string? balanceValue = availableBalance?["value"]?.GetValue<string>();
                        
                        if (!string.IsNullOrEmpty(balanceValue) &&
                            decimal.TryParse(balanceValue, out decimal balanceAmount) && 
                            balanceAmount > 0)
                        {
                            // Found an account with balance > 0, get its details
                            string? uuid = account["uuid"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(uuid))
                            {
                                return await GetAccountByUuidAsync(uuid);
                            }
                        }
                    }
                }
            }            
            // If we couldn't find any suitable account throw new explicit exception
            throw new CoinbaseApiException("No account with a positive balance could be found.");
        }
    }
} 