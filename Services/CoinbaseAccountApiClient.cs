using System.Net.Http.Headers;
using System.Text.Json;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.Responses;
using crypto_bot_api.Helpers;

namespace crypto_bot_api.Services
{
    public class CoinbaseAccountApiClient : ICoinbaseAccountApiClient
    {
        private readonly HttpClient _client;
        private readonly string _baseUrl;
        private readonly string _apiKeyId;
        private readonly string _apiSecret;
        private readonly IConfiguration _configuration;
        private readonly Ed25519JwtHelper _jwtHelper;

        public CoinbaseAccountApiClient(HttpClient client, IConfiguration config)
        {
            _client = client;
            _configuration = config;

            _baseUrl = config["CoinbaseApi:baseUrl"]
                ?? throw new ArgumentNullException(nameof(config), "Coinbase API base URL is not configured.");
            _apiKeyId = config["CoinbaseApi:ApiKeyId"]
                ?? throw new ArgumentNullException(nameof(config), "Coinbase API key ID is not configured.");
            _apiSecret = config["CoinbaseApi:ApiSecret"]
                ?? throw new ArgumentNullException(nameof(config), "Coinbase API secret is not configured.");

            // Create Ed25519 JWT helper - no format validation needed for the API key ID
            _jwtHelper = new Ed25519JwtHelper(_apiKeyId, _apiSecret);
        }

        // Retrieves all Coinbase accounts
        public async Task<string> GetAccountsAsync()
        {
            string endpoint = "/api/v3/brokerage/accounts";
            // Let the Ed25519JwtHelper add the hostname - just pass method and path
            string uri = $"GET {endpoint}";
            string fullUrl = $"{_baseUrl.TrimEnd('/')}{endpoint}";
            
            var jwt = _jwtHelper.GenerateJwt(uri);

            return await SendAuthenticatedGetRequestAsync(jwt, fullUrl, "Failed to retrieve accounts.");
        }

        // Retrieves details for a specific account.
        public async Task<string> GetAccountByUuidAsync(string account_uuid)
        {
            string endpoint = $"/api/v3/brokerage/accounts/{account_uuid}";
            // Let the Ed25519JwtHelper add the hostname - just pass method and path
            string uri = $"GET {endpoint}";
            string fullUrl = $"{_baseUrl.TrimEnd('/')}{endpoint}";
        
            var jwt = _jwtHelper.GenerateJwt(uri);

            return await SendAuthenticatedGetRequestAsync(jwt, fullUrl, $"Failed to retrieve account details for {account_uuid}.");
        }

        public async Task<string> GetAccountDetailsAsync()
        {
            // Get all accounts
            string allAccounts = await GetAccountsAsync();
            
            // Parse the JSON to find an account with balance > 0, currently excludes incremental values in accounts IE: 0.0000000012345
            try
            {
                var accounts = JsonSerializer.Deserialize<JsonElement>(allAccounts);
                
                // Check if accounts data exists and contains accounts
                if (accounts.TryGetProperty("accounts", out var accountsArray) && 
                    accountsArray.ValueKind == JsonValueKind.Array && 
                    accountsArray.GetArrayLength() > 0)
                {
                    // Iterate through accounts to find one with balance > 0
                    for (int i = 0; i < accountsArray.GetArrayLength(); i++)
                    {
                        var account = accountsArray[i];
                        if (account.TryGetProperty("available_balance", out var balance) &&
                            balance.TryGetProperty("value", out var balanceValue))
                        {
                            // Handle possible null value by using the null conditional operator
                            string? balanceStr = balanceValue.ValueKind == JsonValueKind.String 
                                ? balanceValue.GetString() 
                                : null;
                                
                            if (!string.IsNullOrEmpty(balanceStr) && 
                                decimal.TryParse(balanceStr, out decimal balanceAmount) && 
                                balanceAmount > 0)
                            {
                                // Found an account with balance > 0, get its details
                                if (account.TryGetProperty("uuid", out var uuidElement) && 
                                    uuidElement.ValueKind == JsonValueKind.String)
                                {
                                    string? uuid = uuidElement.GetString();
                                    if (!string.IsNullOrEmpty(uuid))
                                    {
                                        // Get detailed information for this account
                                        return await GetAccountByUuidAsync(uuid);
                                    }
                                }
                            }
                        }
                    }
                }
                
                // If we couldn't find any suitable account, return the original response
                return allAccounts;
            }
            catch (JsonException)
            {
                // If there's an error parsing the JSON, return the original response
                return allAccounts;
            }
        }

        // Sends an authenticated GET request to Coinbase and handles errors using our custom error handling.

        private async Task<string> SendAuthenticatedGetRequestAsync(string jwt, string url, string genericErrorMessage)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Add required headers
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            request.Headers.UserAgent.ParseAdd("CryptoTradingBot/1.0");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await _client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Handle Unauthorized responses
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        if (content.Trim().StartsWith("{"))
                        {
                            try
                            {
                                var error = JsonSerializer.Deserialize<CoinbaseErrorResponse>(content);
                                throw new CoinbaseApiException($"Unauthorized: {error?.Message ?? error?.Error ?? error?.ErrorDescription ?? "JWT authentication failed"}");
                            }
                            catch (JsonException)
                            {
                                throw new CoinbaseApiException($"Unauthorized: {content}");
                            }
                        }
                        else
                        {
                            throw new CoinbaseApiException($"Unauthorized: {content}");
                        }
                    }
                    // Handle Not Found
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        throw new CoinbaseApiException($"API endpoint not found: {url}. Response: {content}");
                    }
                    // Handle Bad Request
                    else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        try
                        {
                            var error = JsonSerializer.Deserialize<CoinbaseErrorResponse>(content);
                            throw new CoinbaseApiException($"Bad Request: {error?.Message ?? error?.Error ?? error?.ErrorDescription ?? content}");
                        }
                        catch (JsonException)
                        {
                            throw new CoinbaseApiException($"Bad Request: {content}");
                        }
                    }
                    // For all other errors, try to parse JSON error or use plain text
                    else
                    {
                        try
                        {
                            if (content.Trim().StartsWith("{") || content.Trim().StartsWith("["))
                            {
                                var error = JsonSerializer.Deserialize<CoinbaseErrorResponse>(content);
                                var message = error?.Message ?? error?.Error ?? error?.ErrorDescription ?? genericErrorMessage;
                                throw new CoinbaseApiException($"Coinbase API error: {message}. Status code: {(int)response.StatusCode}");
                            }
                            else
                            {
                                throw new CoinbaseApiException($"Coinbase API error: {content}. Status code: {(int)response.StatusCode}");
                            }
                        }
                        catch (JsonException)
                        {
                            throw new CoinbaseApiException($"{genericErrorMessage} (Response: {content}, Status: {(int)response.StatusCode})");
                        }
                    }
                }

                return content;
            }
            catch (HttpRequestException ex)
            {
                throw new CoinbaseApiException("Transport-level error while calling Coinbase API.", ex);
            }
        }
    }
}