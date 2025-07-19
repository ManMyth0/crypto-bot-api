namespace crypto_bot_api.Services
{
    // Encapsulates sandbox test scenarios as documented in
    // https://docs.cdp.coinbase.com/coinbase-app/advanced-trade-apis/sandbox
    public static class SandboxScenarios
    {
        // Order-related scenarios
        public const string PostOrderInsufficientFund = "PostOrder_insufficient_fund";
        public const string CancelOrdersFailure = "CancelOrders_failure";
        public const string EditOrderFailure = "EditOrder_failure";
        public const string PreviewEditOrderFailure = "PreviewEditOrder_failure";
        public const string PreviewOrderInsufficientFund = "PreviewOrder_insufficient_fund";

        // Order status filters
        public const string OrderStatusCancelled = "CANCELLED";
        public const string OrderStatusOpen = "OPEN";

        // Portfolio types
        public const string PortfolioTypeDefault = "DEFAULT";
        public const string PortfolioTypeConsumer = "CONSUMER";
        public const string PortfolioTypeIntx = "INTX";
    }
} 