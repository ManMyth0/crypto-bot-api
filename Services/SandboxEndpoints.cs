namespace crypto_bot_api.Services
{
    // Defines all Coinbase Advanced Trade API sandbox endpoints.
    public static class SandboxEndpoints
    {
        // Base URL for sandbox
        public const string SandboxBaseUrl = "https://api-sandbox.coinbase.com/api/v3/brokerage";

        public static class Methods
        {
            public const string Get = "GET";
            public const string Post = "POST";
        }

        // Account endpoints - Available in sandbox
        public static class Accounts
        {
            // GET /accounts
            public static class List
            {
                public const string Method = Methods.Get;
                public const string Resource = "/accounts";
            }
            
            // GET /accounts/{account_id}
            public static class Get
            {
                public const string Method = Methods.Get;
                public const string Resource = "/accounts/{account_id}";
                // Parameter: account_id retrieved from List Accounts
            }
        }

        // Order endpoints - Available in sandbox
        public static class Orders
        {
            // POST /orders
            public static class Create
            {
                public const string Method = Methods.Post;
                public const string Resource = "/orders";
            }
            
            // POST /orders/batch_cancel
            public static class BatchCancel
            {
                public const string Method = Methods.Post;
                public const string Resource = "/orders/batch_cancel";
            }
            
            // POST /orders/edit
            public static class Edit
            {
                public const string Method = Methods.Post;
                public const string Resource = "/orders/edit";
            }
            
            // POST /orders/edit_preview
            public static class EditPreview
            {
                public const string Method = Methods.Post;
                public const string Resource = "/orders/edit_preview";
            }
            
            // GET /orders/historical/batch
            public static class ListHistorical
            {
                public const string Method = Methods.Get;
                public const string Resource = "/orders/historical/batch";
                // Parameter: order_status: CANCELLED/OPEN
            }
            
            // GET /orders/historical/fills
            public static class ListFills
            {
                public const string Method = Methods.Get;
                public const string Resource = "/orders/historical/fills";
            }
            
            // GET /orders/historical/{order_id}
            public static class GetHistorical
            {
                public const string Method = Methods.Get;
                public const string Resource = "/orders/historical/{order_id}";
                // Parameter: order_id retrieved from List Orders
            }
            
            // POST /orders/preview
            public static class Preview
            {
                public const string Method = Methods.Post;
                public const string Resource = "/orders/preview";
            }
            
            // POST /orders/close_position
            public static class ClosePosition
            {
                public const string Method = Methods.Post;
                public const string Resource = "/orders/close_position";
            }
        }

        // Portfolio endpoints - Doc states only Accounts and Orders available in sandbox
        public static class Portfolios
        {
            // GET /portfolios
            public static class List
            {
                public const string Method = Methods.Get;
                public const string Resource = "/portfolios";
                // Parameter: portfolio_type: DEFAULT/CONSUMER/INTX
            }
        }

        // Perpetuals endpoints - Doc states only Accounts and Orders available in sandbox
        public static class Perpetuals
        {
            // POST intx/allocate (no leading slash in docs)
            public static class Allocate
            {
                public const string Method = Methods.Post;
                public const string Resource = "intx/allocate";
                // Parameter: portfolio_uuid retrieved from List Portfolios
            }
            
            // GET /intx/portfolio/{portfolio_uuid}
            public static class GetPortfolioSummary
            {
                public const string Method = Methods.Get;
                public const string Resource = "/intx/portfolio/{portfolio_uuid}";
                // Parameter: portfolio_uuid retrieved from List Portfolios
            }
            
            // GET /intx/positions/{portfolio_uuid}
            public static class ListPositions
            {
                public const string Method = Methods.Get;
                public const string Resource = "/intx/positions/{portfolio_uuid}";
                // Parameter: portfolio_uuid retrieved from List Portfolios
            }
            
            // GET /intx/positions/{portfolio_uuid}/{symbol}
            public static class GetPosition
            {
                public const string Method = Methods.Get;
                public const string Resource = "/intx/positions/{portfolio_uuid}/{symbol}";
                // Parameters: 
                // - portfolio_uuid retrieved from List Portfolios
                // - symbol: e.g. ETH-PERP-INTX
            }
            
            // GET /intx/balances/{portfolio_uuid}
            public static class GetBalances
            {
                public const string Method = Methods.Get;
                public const string Resource = "/intx/balances/{portfolio_uuid}";
                // Parameter: portfolio_uuid retrieved from List Portfolios
            }
            
            // POST /intx/multi_asset_collateral
            public static class MultiAssetCollateral
            {
                public const string Method = Methods.Post;
                public const string Resource = "/intx/multi_asset_collateral";
                // Parameter: portfolio_uuid retrieved from List Portfolios
            }
        }

        // Helper method to check if endpoint is documented as sandbox-supported
        public static bool IsSandboxSupported(string endpoint)
        {
            // According to docs: "Only Accounts and Orders related endpoints are currently available in sandbox"
            return endpoint.StartsWith("/accounts") || 
                   endpoint.StartsWith("/orders");
        }

        #region Request Parameters
        public static class RequestParameters
        {
            // GET /accounts/{account_id}
            public static class GetAccount
            {
                public const string Method = Methods.Get;
                public const string Resource = "/accounts/{account_id}";
                public const string Description = "account_id retrieved from List Accounts";
            }

            // GET /orders/historical/{order_id}
            public static class GetOrder
            {
                public const string Method = Methods.Get;
                public const string Resource = "/orders/historical/{order_id}";
                public const string Description = "order_id retrieved from List Orders";
            }

            // GET /orders/historical/batch
            public static class ListOrders
            {
                public const string Method = Methods.Get;
                public const string Resource = "/orders/historical/batch";
                public const string Description = "order_status: CANCELLED/OPEN";
                public const string Cancelled = "CANCELLED";
                public const string Open = "OPEN";
            }

            // GET /portfolios
            public static class ListPortfolios
            {
                public const string Method = Methods.Get;
                public const string Resource = "/portfolios";
                public const string Description = "portfolio_type: DEFAULT/CONSUMER/INTX";
                public const string Default = "DEFAULT";
                public const string Consumer = "CONSUMER";
                public const string Intx = "INTX";
            }
        }
        #endregion

        #region Error Responses
        public static class ErrorResponses
        {
            public static class Types
            {
                public const string InsufficientFund = "INSUFFICIENT_FUND";
                public const string UnknownCancelOrder = "UNKNOWN_CANCEL_ORDER";
                public const string OrderNotFound = "ORDER_NOT_FOUND";
                public const string PreviewInsufficientFund = "PREVIEW_INSUFFICIENT_FUND";
            }

            public static class Headers
            {
                public static class CreateOrder
                {
                    public const string Method = Methods.Post;
                    public const string Resource = "/orders";
                    public const string Error = Types.InsufficientFund;
                    public const string Header = "X-Sandbox: PostOrder_insufficient_fund";
                }

                public static class CancelOrders
                {
                    public const string Method = Methods.Post;
                    public const string Resource = "/orders/batch_cancel";
                    public const string Error = Types.UnknownCancelOrder;
                    public const string Header = "X-Sandbox: CancelOrders_failure";
                }

                public static class EditOrder
                {
                    public const string Method = Methods.Post;
                    public const string Resource = "/orders/edit";
                    public const string Error = Types.OrderNotFound;
                    public const string Header = "X-Sandbox: EditOrder_failure";
                }

                public static class EditOrderPreview
                {
                    public const string Method = Methods.Post;
                    public const string Resource = "/orders/edit_preview";
                    public const string Error = Types.OrderNotFound;
                    public const string Header = "X-Sandbox: PreviewEditOrder_failure";
                }

                public static class PreviewOrder
                {
                    public const string Method = Methods.Post;
                    public const string Resource = "/orders/preview";
                    public const string Error = Types.PreviewInsufficientFund;
                    public const string Header = "X-Sandbox: PreviewOrder_insufficient_fund";
                }
            }
        }
        #endregion
    }
} 