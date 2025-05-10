using System.Net.Mime;
using crypto_bot_api.Services;
using Microsoft.AspNetCore.Mvc;
using crypto_bot_api.Models.DTOs;
using crypto_bot_api.CustomExceptions;

namespace crypto_bot_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoinbaseAccountController : ControllerBase
    {
        private readonly ICoinbaseAccountApiClient _coinbaseClient;
        private readonly IConfiguration _configuration;

        public CoinbaseAccountController(ICoinbaseAccountApiClient coinbaseClient, IConfiguration configuration)
        {
            _coinbaseClient = coinbaseClient;
            _configuration = configuration;
        }

        [HttpGet("accounts")]
        public async Task<ActionResult<AccountsResponseDto>> GetAccounts()
        {
            try
            {
                var result = await _coinbaseClient.GetAccountsAsync();
                return result;
            }
            catch (CoinbaseApiException ex)
            {
                return StatusCode(502, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("account-details")]
        public async Task<ActionResult<AccountDetailResponseDto>> GetAccountDetails()
        {
            try
            {
                var result = await _coinbaseClient.GetAccountDetailsAsync();
                if (result == null)
                {
                    return NotFound(new { error = "No account found with positive balance" });
                }
                return result;
            }
            catch (CoinbaseApiException ex)
            {
                return StatusCode(502, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("account/{accountId}")]
        public async Task<ActionResult<AccountDetailResponseDto>> GetAccountById(string accountId)
        {
            try
            {
                var result = await _coinbaseClient.GetAccountByUuidAsync(accountId);
                return result;
            }
            catch (CoinbaseApiException ex)
            {
                return StatusCode(502, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("config")]
        public IActionResult GetConfigInfo()
        {
            var baseUrl = _configuration["CoinbaseApi:baseUrl"];
            var apiKeyId = _configuration["CoinbaseApi:ApiKeyId"];
            
            return Ok(new
            {
                BaseUrl = baseUrl,
                ApiKeyId = apiKeyId,
                ApiSecretLength = _configuration["CoinbaseApi:ApiSecret"]?.Length ?? 0
            });
        }
    }
} 