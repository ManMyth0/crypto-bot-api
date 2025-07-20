using crypto_bot_api.Services;
using Microsoft.Extensions.Options;

namespace crypto_bot_api.Middleware
{
    public interface ISandboxHeaderValidator
    {
        bool IsValidScenario(string scenario);
    }

    public class SandboxHeaderValidator : ISandboxHeaderValidator
    {
        public bool IsValidScenario(string scenario)
        {
            return scenario == SandboxScenarios.PostOrderInsufficientFund ||
                   scenario == SandboxScenarios.CancelOrdersFailure ||
                   scenario == SandboxScenarios.EditOrderFailure ||
                   scenario == SandboxScenarios.PreviewEditOrderFailure ||
                   scenario == SandboxScenarios.PreviewOrderInsufficientFund;
        }
    }

    public class SandboxMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly bool _isSandbox;
        private readonly ISandboxHeaderValidator _validator;

        public SandboxMiddleware(
            RequestDelegate next, 
            IOptions<SandboxConfiguration> config,
            ISandboxHeaderValidator validator)
        {
            _next = next;
            _isSandbox = config.Value.Enabled;
            _validator = validator;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_isSandbox)
            {
                context.Request.Headers.Remove("X-Sandbox");
                await _next(context);
                return;
            }

            if (context.Request.Headers.TryGetValue("X-Sandbox", out var sandboxHeader))
            {
                var scenario = sandboxHeader.ToString();
                if (!_validator.IsValidScenario(scenario))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new { error = "Invalid sandbox scenario" });
                    return;
                }
            }
            
            await _next(context);
        }
    }

    public static class SandboxMiddlewareExtensions
    {
        public static IApplicationBuilder UseSandboxHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SandboxMiddleware>();
        }

        public static IServiceCollection AddSandboxServices(this IServiceCollection services)
        {
            services.AddScoped<ISandboxHeaderValidator, SandboxHeaderValidator>();
            return services;
        }
    }
} 