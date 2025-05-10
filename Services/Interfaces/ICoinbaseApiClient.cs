namespace crypto_bot_api.Services
{
    public interface ICoinbaseAccountApiClient
    {
        Task<string> GetAccountsAsync();
        Task<string> GetAccountDetailsAsync();
        Task<string> GetAccountByUuidAsync(string account_uuid);
    }
}