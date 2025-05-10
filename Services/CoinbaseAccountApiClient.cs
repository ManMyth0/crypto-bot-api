using System;
using System.Net.Http;
using System.Text.Json;
using crypto_bot_api.Helpers;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using crypto_bot_api.Models.DTOs;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.Responses;
using Microsoft.Extensions.Configuration;

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
        public async Task<AccountsResponseDto> GetAccountsAsync()
        {
            string endpoint = "/api/v3/brokerage/accounts";
            // Let the Ed25519JwtHelper add the hostname - just pass method and path
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
            // Let the Ed25519JwtHelper add the hostname - just pass method and path
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