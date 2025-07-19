// Base class for Coinbase API clients
using System.Text;
using System.Text.Json;
using crypto_bot_api.Helpers;
using System.Net.Http.Headers;
using crypto_bot_api.Models.Responses;
using crypto_bot_api.CustomExceptions;

namespace crypto_bot_api.Services
{
    public abstract class BaseCoinbaseApiClient
    {
        protected readonly HttpClient _client;
        protected readonly string _baseUrl;
        protected readonly string _apiKeyId;
        protected readonly string _apiSecret;
        protected readonly IConfiguration _configuration;
        protected readonly Ed25519JwtHelper _jwtHelper;

        protected BaseCoinbaseApiClient(HttpClient client, IConfiguration config)
        {
            _client = client;
            _configuration = config;

            _baseUrl = config["CoinbaseApi:baseUrl"]
                ?? throw new ArgumentNullException(nameof(config), "Coinbase API base URL is not configured.");
            _apiKeyId = config["CoinbaseApi:ApiKeyId"]
                ?? throw new ArgumentNullException(nameof(config), "Coinbase API key ID is not configured.");
            _apiSecret = config["CoinbaseApi:ApiSecret"]
                ?? throw new ArgumentNullException(nameof(config), "Coinbase API secret is not configured.");

            // Create Ed25519 JWT helper
            _jwtHelper = new Ed25519JwtHelper(_apiKeyId, _apiSecret);
        }

        protected async Task<string> SendAuthenticatedGetRequestAsync(string jwt, string url, string genericErrorMessage)
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
        
        protected async Task<string> SendAuthenticatedPostRequestAsync(string jwt, string url, string content, string genericErrorMessage)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            // Add required headers
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            request.Headers.UserAgent.ParseAdd("CryptoTradingBot/1.0");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            // Add the JSON content
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");

            try
            {
                var response = await _client.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Handle Unauthorized responses
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        if (responseContent.Trim().StartsWith("{"))
                        {
                            try
                            {
                                var error = JsonSerializer.Deserialize<CoinbaseErrorResponse>(responseContent);
                                throw new CoinbaseApiException($"Unauthorized: {error?.Message ?? error?.Error ?? error?.ErrorDescription ?? "JWT authentication failed"}");
                            }
                            catch (JsonException)
                            {
                                throw new CoinbaseApiException($"Unauthorized: {responseContent}");
                            }
                        }
                        else
                        {
                            throw new CoinbaseApiException($"Unauthorized: {responseContent}");
                        }
                    }
                    // Handle Not Found
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        throw new CoinbaseApiException($"API endpoint not found: {url}. Response: {responseContent}");
                    }
                    // Handle Bad Request
                    else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        try
                        {
                            var error = JsonSerializer.Deserialize<CoinbaseErrorResponse>(responseContent);
                            throw new CoinbaseApiException($"Bad Request: {error?.Message ?? error?.Error ?? error?.ErrorDescription ?? responseContent}");
                        }
                        catch (JsonException)
                        {
                            throw new CoinbaseApiException($"Bad Request: {responseContent}");
                        }
                    }
                    // For all other errors, try to parse JSON error or use plain text
                    else
                    {
                        try
                        {
                            if (responseContent.Trim().StartsWith("{") || responseContent.Trim().StartsWith("["))
                            {
                                var error = JsonSerializer.Deserialize<CoinbaseErrorResponse>(responseContent);
                                var message = error?.Message ?? error?.Error ?? error?.ErrorDescription ?? genericErrorMessage;
                                throw new CoinbaseApiException($"Coinbase API error: {message}. Status code: {(int)response.StatusCode}");
                            }
                            else
                            {
                                throw new CoinbaseApiException($"Coinbase API error: {responseContent}. Status code: {(int)response.StatusCode}");
                            }
                        }
                        catch (JsonException)
                        {
                            throw new CoinbaseApiException($"{genericErrorMessage} (Response: {responseContent}, Status: {(int)response.StatusCode})");
                        }
                    }
                }

                return responseContent;
            }
            catch (HttpRequestException ex)
            {
                throw new CoinbaseApiException("Transport-level error while calling Coinbase API.", ex);
            }
        }
    }
} 