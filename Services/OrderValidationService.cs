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
                result.IsValid = false;
                return result;
            }

            result.ProductId = productInfo.ProductId;
            result.Status = productInfo.Status;
            result.TradingDisabled = productInfo.TradingDisabled;

            // Check trading status
            if (productInfo.TradingDisabled || productInfo.Status?.ToLower() == "offline")
            {
                result.Warnings.Add($"Trading may be disabled for {orderRequest.ProductId} (Status: {productInfo.Status}, TradingDisabled: {productInfo.TradingDisabled})");
                result.IsValid = false;
            }

            if (productInfo.Status?.ToLower() == "delisted")
            {
                result.Warnings.Add($"Product {orderRequest.ProductId} appears to be delisted");
                result.IsValid = false;
            }

            // Get order size from configuration
            string? baseSize = null;
            string? quoteSize = null;
            bool isMarketOrder = false;

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
            else if (orderRequest.OrderConfiguration?.MarketMarketIoc != null)
            {
                if (productInfo.LimitOnly)
                {
                    result.Warnings.Add($"Product {orderRequest.ProductId} accepts limit orders only");
                    result.IsValid = false;
                }
                baseSize = orderRequest.OrderConfiguration.MarketMarketIoc.BaseSize;
                quoteSize = orderRequest.OrderConfiguration.MarketMarketIoc.QuoteSize;
                isMarketOrder = true;
            }
            else
            {
                result.Warnings.Add("Invalid order configuration");
                result.IsValid = false;
                return result;
            }

            // For market orders, only quote_size is required
            if (isMarketOrder)
            {
                if (string.IsNullOrEmpty(quoteSize))
                {
                    result.Warnings.Add("QuoteSize is required for market orders");
                    result.IsValid = false;
                    return result;
                }
            }
            // For limit orders, either base_size OR quote_size is required (not both)
            else
            {
                if (string.IsNullOrEmpty(baseSize) && string.IsNullOrEmpty(quoteSize))
                {
                    result.Warnings.Add("Either BaseSize or QuoteSize is required for limit orders");
                    result.IsValid = false;
                    return result;
                }
                if (!string.IsNullOrEmpty(baseSize) && !string.IsNullOrEmpty(quoteSize))
                {
                    result.Warnings.Add("Only one of BaseSize or QuoteSize should be provided for limit orders, not both");
                    result.IsValid = false;
                    return result;
                }
            }

            // Validate the fields that are actually provided
            if (!string.IsNullOrEmpty(baseSize))
            {
                if (decimal.TryParse(baseSize, out decimal baseSizeValue))
                {
                    // Check base_size increment
                    var remainder = baseSizeValue % productInfo.BaseIncrement;
                    if (remainder != 0)
                    {
                        result.Warnings.Add($"Base size {baseSizeValue} is not an increment of {productInfo.BaseIncrement}");
                        result.IsValid = false;
                    }
                }
                else
                {
                    result.Warnings.Add("Invalid base_size format");
                    result.IsValid = false;
                }
            }

            if (!string.IsNullOrEmpty(quoteSize))
            {
                if (decimal.TryParse(quoteSize, out decimal quoteSizeValue))
                {
                    // Check minimum funds requirement
                    if (quoteSizeValue < productInfo.MinMarketFunds)
                    {
                        result.Warnings.Add($"Order value {quoteSizeValue} is below minimum {productInfo.MinMarketFunds}");
                        result.IsValid = false;
                    }
                }
                else
                {
                    result.Warnings.Add("Invalid quote_size format");
                    result.IsValid = false;
                }
            }

            _logger.LogInformation("Order validation completed for {ProductId} with {WarningCount} warnings", 
                orderRequest.ProductId, result.Warnings.Count);

            return result;
        }
    }
} 