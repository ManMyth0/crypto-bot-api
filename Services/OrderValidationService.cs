using crypto_bot_api.Models;
using crypto_bot_api.Models.DTOs.Orders;

namespace crypto_bot_api.Services
{
    public interface IOrderValidationService
    {
        Task<ValidationResult> ValidateOrderAsync(CreateOrderRequestDto orderRequest);
    }

    public class OrderValidationService : IOrderValidationService
    {
        private readonly IProductInfoService _productInfoService;
        private readonly ILogger<OrderValidationService> _logger;

        public OrderValidationService(
            IProductInfoService productInfoService,
            ILogger<OrderValidationService> logger)
        {
            _productInfoService = productInfoService;
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateOrderAsync(CreateOrderRequestDto orderRequest)
        {
            var result = new ValidationResult
            {
                ValidationTimestamp = DateTime.UtcNow
            };

            if (orderRequest == null)
            {
                result.IsValid = false;
                result.Warnings.Add("Order request is null");
                return result;
            }

            if (string.IsNullOrEmpty(orderRequest.ProductId))
            {
                result.IsValid = false;
                result.Warnings.Add("ProductId is required");
                return result;
            }

            var productInfo = await _productInfoService.GetProductInfoAsync(orderRequest.ProductId);
            if (productInfo == null)
            {
                result.Warnings.Add($"Product {orderRequest.ProductId} not found or unavailable");
                return result;
            }

            result.ProductId = productInfo.ProductId;
            result.Status = productInfo.Status;
            result.TradingDisabled = productInfo.TradingDisabled;

            // Check trading status
            if (productInfo.TradingDisabled || productInfo.Status?.ToLower() == "offline")
            {
                result.Warnings.Add($"Trading may be disabled for {orderRequest.ProductId} (Status: {productInfo.Status}, TradingDisabled: {productInfo.TradingDisabled})");
            }

            if (productInfo.Status?.ToLower() == "delisted")
            {
                result.Warnings.Add($"Product {orderRequest.ProductId} appears to be delisted");
            }

            // Get order size from configuration
            string? baseSize = null;
            string? quoteSize = null;

            if (orderRequest.OrderConfiguration?.LimitLimitGtc != null)
            {
                baseSize = orderRequest.OrderConfiguration.LimitLimitGtc.BaseSize;
                quoteSize = orderRequest.OrderConfiguration.LimitLimitGtc.QuoteSize;
            }
            else if (orderRequest.OrderConfiguration?.LimitLimitGtd != null)
            {
                baseSize = orderRequest.OrderConfiguration.LimitLimitGtd.BaseSize;
                quoteSize = orderRequest.OrderConfiguration.LimitLimitGtd.QuoteSize;
            }
            else if (orderRequest.OrderConfiguration?.MarketMarket != null)
            {
                if (productInfo.LimitOnly)
                {
                    result.Warnings.Add($"Product {orderRequest.ProductId} accepts limit orders only");
                }
                baseSize = orderRequest.OrderConfiguration.MarketMarket.BaseSize;
                quoteSize = orderRequest.OrderConfiguration.MarketMarket.QuoteSize;
            }
            else
            {
                result.Warnings.Add("Invalid order configuration");
                return result;
            }

            if (string.IsNullOrEmpty(baseSize))
            {
                result.Warnings.Add("BaseSize is required");
                return result;
            }

            if (string.IsNullOrEmpty(quoteSize))
            {
                result.Warnings.Add("QuoteSize is required");
                return result;
            }

            // Validate base_size increment
            if (decimal.TryParse(baseSize, out decimal baseSizeValue))
            {
                var remainder = baseSizeValue % productInfo.BaseIncrement;
                if (remainder != 0)
                {
                    result.Warnings.Add($"Base size {baseSizeValue} is not an increment of {productInfo.BaseIncrement}");
                }
            }
            else
            {
                result.Warnings.Add("Invalid base_size format");
            }

            // Validate minimum order value
            if (decimal.TryParse(quoteSize, out decimal quoteSizeValue))
            {
                if (quoteSizeValue < productInfo.MinMarketFunds)
                {
                    result.Warnings.Add($"Order value {quoteSizeValue} is below minimum {productInfo.MinMarketFunds}");
                }
            }
            else
            {
                result.Warnings.Add("Invalid quote_size format");
            }

            _logger.LogInformation("Order validation completed for {ProductId} with {WarningCount} warnings", 
                orderRequest.ProductId, result.Warnings.Count);

            return result;
        }
    }
} 