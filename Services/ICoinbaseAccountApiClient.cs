using crypto_bot_api.Models.DTOs;

namespace crypto_bot_api.Services
{
    public interface ICoinbaseAccountApiClient
    {
        Task<AccountsResponseDto> GetAccountsAsync();
        Task<AccountDetailResponseDto?> GetAccountDetailsAsync();
        Task<AccountDetailResponseDto> GetAccountByUuidAsync(string account_uuid);
    }
} 