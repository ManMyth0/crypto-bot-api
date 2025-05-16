using System.Text.Json.Nodes;

namespace crypto_bot_api.Services
{
    public interface ICoinbaseAccountApiClient
    {
        Task<JsonObject> GetAccountsAsync();
        Task<JsonObject> GetAccountDetailsAsync();
        Task<JsonObject> GetAccountByUuidAsync(string account_uuid);
    }
} 