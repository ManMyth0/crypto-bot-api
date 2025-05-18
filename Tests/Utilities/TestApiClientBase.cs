using Moq;
using System.Net;
using System.Text;
using Moq.Protected;
using System.Text.Json;
using crypto_bot_api.CustomExceptions;

namespace crypto_bot_api.Tests.Utilities
{
    public abstract class TestApiClientBase
    {
        protected readonly HttpClient _client;
        protected readonly Mock<HttpMessageHandler> _mockHandler;
        protected readonly string _baseUrl = "https://api.coinbase.com";
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        protected TestApiClientBase(Mock<HttpMessageHandler> mockHandler)
        {
            _mockHandler = mockHandler;
            _client = new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = DefaultTimeout
            };
        }

        protected void SetupHttpResponseForMethod(HttpStatusCode statusCode, string content, string urlContains, HttpMethod method)
        {
            _mockHandler
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

        protected async Task<T> SendRequestAsync<T>(HttpMethod method, string endpoint, object? content = null)
        {
            using var cts = new CancellationTokenSource(DefaultTimeout);
            string fullUrl = $"{_baseUrl}{endpoint}";
            var request = new HttpRequestMessage(method, fullUrl);

            if (content != null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(content),
                    Encoding.UTF8,
                    "application/json");
            }

            var response = await _client.SendAsync(request, cts.Token);
            string jsonResponse = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                throw new CoinbaseApiException(jsonResponse);
            }

            return JsonSerializer.Deserialize<T>(
                jsonResponse,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                ?? throw new InvalidOperationException("Failed to deserialize response");
        }
    }
} 