namespace crypto_bot_api.Services
{
    // Configuration settings for the Coinbase Advanced Trade API sandbox environment
    public class SandboxConfiguration
    {
        public const string ConfigurationSection = "CoinbaseApi:Sandbox";

        // Whether to use the sandbox environment
        public bool Enabled { get; set; }

        // Optional test scenario to trigger specific error responses
        public string? TestScenario { get; set; }

        // Validates a request against sandbox documentation requirements
        public static (bool isValid, string? error) ValidateRequest(
            string resource,
            string method,
            IDictionary<string, string>? parameters = null,
            string? errorScenario = null)
        {
            // Validate method
            if (method != SandboxEndpoints.Methods.Get && method != SandboxEndpoints.Methods.Post)
            {
                return (false, $"Invalid HTTP method '{method}'. Only GET and POST are supported.");
            }

            // Check if endpoint exists in documentation
            if (!IsDocumentedEndpoint(resource, method))
            {
                return (false, $"Endpoint '{method} {resource}' is not documented in the API.");
            }

            // Check if endpoint is available in sandbox
            if (!SandboxEndpoints.IsSandboxSupported(resource))
            {
                return (false, "Only Accounts and Orders related endpoints are currently available in sandbox.");
            }

            // Validate parameters if provided
            if (parameters != null)
            {
                var (isValid, error) = ValidateParameters(resource, parameters);
                if (!isValid) return (false, error);
            }

            // Validate error scenario if provided
            if (!string.IsNullOrEmpty(errorScenario))
            {
                var (isValid, error) = ValidateErrorScenario(resource, method, errorScenario);
                if (!isValid) return (false, error);
            }

            return (true, null);
        }

        private static bool IsDocumentedEndpoint(string resource, string method)
        {
            // Check all documented endpoints
            return
                // Accounts
                (resource == SandboxEndpoints.Accounts.List.Resource && method == SandboxEndpoints.Accounts.List.Method) ||
                (resource == SandboxEndpoints.Accounts.Get.Resource && method == SandboxEndpoints.Accounts.Get.Method) ||
                
                // Orders
                (resource == SandboxEndpoints.Orders.Create.Resource && method == SandboxEndpoints.Orders.Create.Method) ||
                (resource == SandboxEndpoints.Orders.BatchCancel.Resource && method == SandboxEndpoints.Orders.BatchCancel.Method) ||
                (resource == SandboxEndpoints.Orders.Edit.Resource && method == SandboxEndpoints.Orders.Edit.Method) ||
                (resource == SandboxEndpoints.Orders.EditPreview.Resource && method == SandboxEndpoints.Orders.EditPreview.Method) ||
                (resource == SandboxEndpoints.Orders.ListHistorical.Resource && method == SandboxEndpoints.Orders.ListHistorical.Method) ||
                (resource == SandboxEndpoints.Orders.ListFills.Resource && method == SandboxEndpoints.Orders.ListFills.Method) ||
                (resource == SandboxEndpoints.Orders.GetHistorical.Resource && method == SandboxEndpoints.Orders.GetHistorical.Method) ||
                (resource == SandboxEndpoints.Orders.Preview.Resource && method == SandboxEndpoints.Orders.Preview.Method) ||
                (resource == SandboxEndpoints.Orders.ClosePosition.Resource && method == SandboxEndpoints.Orders.ClosePosition.Method) ||

                // Portfolios
                (resource == SandboxEndpoints.Portfolios.List.Resource && method == SandboxEndpoints.Portfolios.List.Method) ||

                // Perpetuals
                (resource == SandboxEndpoints.Perpetuals.Allocate.Resource && method == SandboxEndpoints.Perpetuals.Allocate.Method) ||
                (resource == SandboxEndpoints.Perpetuals.GetPortfolioSummary.Resource && method == SandboxEndpoints.Perpetuals.GetPortfolioSummary.Method) ||
                (resource == SandboxEndpoints.Perpetuals.ListPositions.Resource && method == SandboxEndpoints.Perpetuals.ListPositions.Method) ||
                (resource == SandboxEndpoints.Perpetuals.GetPosition.Resource && method == SandboxEndpoints.Perpetuals.GetPosition.Method) ||
                (resource == SandboxEndpoints.Perpetuals.GetBalances.Resource && method == SandboxEndpoints.Perpetuals.GetBalances.Method) ||
                (resource == SandboxEndpoints.Perpetuals.MultiAssetCollateral.Resource && method == SandboxEndpoints.Perpetuals.MultiAssetCollateral.Method);
        }

        private static (bool isValid, string? error) ValidateParameters(string resource, IDictionary<string, string> parameters)
        {
            // Match endpoint to its required parameters
            var endpoint = typeof(SandboxEndpoints.RequestParameters).GetNestedTypes()
                .FirstOrDefault(t => t.GetField("Resource")?.GetValue(null)?.ToString() == resource);

            if (endpoint == null)
            {
                return (true, null); // Endpoint doesn't require parameters
            }

            var description = endpoint.GetField("Description")?.GetValue(null)?.ToString();
            
            // Validate based on parameter type
            if (description?.Contains("account_id") == true)
            {
                return ValidateAccountId(parameters);
            }
            if (description?.Contains("order_id") == true)
            {
                return ValidateOrderId(parameters);
            }
            if (description?.Contains("order_status") == true)
            {
                return ValidateOrderStatus(parameters);
            }
            if (description?.Contains("portfolio_type") == true)
            {
                return ValidatePortfolioType(parameters);
            }
            if (description?.Contains("portfolio_uuid") == true)
            {
                var result = ValidatePortfolioUuid(parameters);
                if (!result.isValid) return result;

                // Check for additional symbol parameter if required
                if (resource.Contains("{symbol}") && !parameters.ContainsKey("symbol"))
                {
                    return (false, "symbol parameter is required (e.g. ETH-PERP-INTX)");
                }
            }

            return (true, null);
        }

        private static (bool isValid, string? error) ValidateErrorScenario(string resource, string method, string scenario)
        {
            // Only certain POST endpoints support error scenarios
            var errorEndpoint = typeof(SandboxEndpoints.ErrorResponses.Headers).GetNestedTypes()
                .FirstOrDefault(t => 
                    t.GetField("Resource")?.GetValue(null)?.ToString() == resource &&
                    t.GetField("Method")?.GetValue(null)?.ToString() == method);

            if (errorEndpoint == null)
            {
                return (false, $"Endpoint '{method} {resource}' does not support error scenarios");
            }

            var expectedHeader = errorEndpoint.GetField("Header")?.GetValue(null)?.ToString();
            if (scenario != expectedHeader)
            {
                return (false, $"Invalid error scenario. Expected: {expectedHeader}");
            }

            return (true, null);
        }

        private static (bool isValid, string? error) ValidateAccountId(IDictionary<string, string> parameters)
        {
            return parameters.ContainsKey("account_id")
                ? (true, null)
                : (false, "account_id is required and must be retrieved from List Accounts");
        }

        private static (bool isValid, string? error) ValidateOrderId(IDictionary<string, string> parameters)
        {
            return parameters.ContainsKey("order_id")
                ? (true, null)
                : (false, "order_id is required and must be retrieved from List Orders");
        }

        private static (bool isValid, string? error) ValidateOrderStatus(IDictionary<string, string> parameters)
        {
            if (!parameters.ContainsKey("order_status"))
            {
                return (false, "order_status is required");
            }

            var status = parameters["order_status"];
            return status == SandboxEndpoints.RequestParameters.ListOrders.Cancelled ||
                   status == SandboxEndpoints.RequestParameters.ListOrders.Open
                ? (true, null)
                : (false, "order_status must be either CANCELLED or OPEN");
        }

        private static (bool isValid, string? error) ValidatePortfolioType(IDictionary<string, string> parameters)
        {
            if (!parameters.ContainsKey("portfolio_type"))
            {
                return (false, "portfolio_type is required");
            }

            var type = parameters["portfolio_type"];
            return type == SandboxEndpoints.RequestParameters.ListPortfolios.Default ||
                   type == SandboxEndpoints.RequestParameters.ListPortfolios.Consumer ||
                   type == SandboxEndpoints.RequestParameters.ListPortfolios.Intx
                ? (true, null)
                : (false, "portfolio_type must be DEFAULT, CONSUMER, or INTX");
        }

        private static (bool isValid, string? error) ValidatePortfolioUuid(IDictionary<string, string> parameters)
        {
            return parameters.ContainsKey("portfolio_uuid")
                ? (true, null)
                : (false, "portfolio_uuid is required and must be retrieved from List Portfolios");
        }
    }
} 