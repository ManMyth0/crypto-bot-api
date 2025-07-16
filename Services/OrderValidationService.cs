using System;
using System.Threading.Tasks;
using crypto_bot_api.CustomExceptions;
using crypto_bot_api.Models.DTOs.Orders;
using Microsoft.Extensions.Logging;

namespace crypto_bot_api.Services
{
    public interface IOrderValidationService
    {
        Task ValidateOrderAsync(CreateOrderRequestDto orderRequest);
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

        public async Task ValidateOrderAsync(CreateOrderRequestDto orderRequest)
        {
            if (orderRequest == null)
            {
                throw new ArgumentNullException(nameof(orderRequest));
            }

            if (string.IsNullOrEmpty(orderRequest.ProductId))
            {
                throw new CoinbaseApiException("ProductId is required");
            }

            var productInfo = await _productInfoService.GetProductInfoAsync(orderRequest.ProductId);
            if (productInfo == null)
            {
                throw new CoinbaseApiException($"Product {orderRequest.ProductId} not found or unavailable");
            }

            if (productInfo.TradingDisabled || productInfo.Status?.ToLower() == "offline")
            {
                throw new CoinbaseApiException($"Trading is disabled for {orderRequest.ProductId}");
            }

            if (productInfo.Status?.ToLower() == "delisted")
            {
                throw new CoinbaseApiException($"Product {orderRequest.ProductId} has been delisted");
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
                    throw new CoinbaseApiException($"Product {orderRequest.ProductId} accepts limit orders only");
                }
                baseSize = orderRequest.OrderConfiguration.MarketMarket.BaseSize;
                quoteSize = orderRequest.OrderConfiguration.MarketMarket.QuoteSize;
            }
            else
            {
                throw new CoinbaseApiException("Invalid order configuration");
            }

            if (string.IsNullOrEmpty(baseSize))
            {
                throw new CoinbaseApiException("BaseSize is required");
            }

            if (string.IsNullOrEmpty(quoteSize))
            {
                throw new CoinbaseApiException("QuoteSize is required");
            }

            // Validate base_size increment
            if (decimal.TryParse(baseSize, out decimal baseSizeValue))
            {
                var remainder = baseSizeValue % productInfo.BaseIncrement;
                if (remainder != 0)
                {
                    throw new CoinbaseApiException(
                        $"Invalid base_size. Must be an increment of {productInfo.BaseIncrement}");
                }
            }
            else
            {
                throw new CoinbaseApiException("Invalid base_size format");
            }

            // Validate minimum order value
            if (decimal.TryParse(quoteSize, out decimal quoteSizeValue))
            {
                if (quoteSizeValue < productInfo.MinMarketFunds)
                {
                    throw new CoinbaseApiException(
                        $"Order value {quoteSizeValue} is below minimum {productInfo.MinMarketFunds}");
                }
            }
            else
            {
                throw new CoinbaseApiException("Invalid quote_size format");
            }

            _logger.LogInformation($"Order validation passed for {orderRequest.ProductId}");
        }
    }
} 