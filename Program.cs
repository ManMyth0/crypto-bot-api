using crypto_bot_api.Data;
using crypto_bot_api.Services;
using crypto_bot_api.Services.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Enhanced logging for debugging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Load user secrets before adding services
builder.Configuration.AddUserSecrets<Program>();

// Get configuration values for proper display
var baseUrl = builder.Configuration["CoinbaseApi:baseUrl"];
var apiKeyId = builder.Configuration["CoinbaseApi:ApiKeyId"];
var apiSecret = builder.Configuration["CoinbaseApi:ApiSecret"];

// Add services to the container
builder.Services.AddAuthorization();

// Register HttpClient with rate limiting
builder.Services.AddCoinbaseRateLimiting(builder.Configuration);

// Retrieve the connection string from user secrets
var connectionString = builder.Configuration["PostgresLocalDatabaseConnection"];

// Register your DbContext to use PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register services for Coinbase API clients with rate limiting
builder.Services.AddCoinbaseHttpClient<ICoinbaseAccountApiClient, CoinbaseAccountApiClient>();
builder.Services.AddCoinbaseHttpClient<ICoinbaseOrderApiClient, CoinbaseOrderApiClient>();

// Register AssembleOrderDetailsService
builder.Services.AddScoped<IAssembleOrderDetailsService, AssembleOrderDetailsService>();

// Register Position Management Services
builder.Services.AddSingleton<TradeMetricsCalculator>();
builder.Services.AddScoped<IPositionManagementService, PositionManagementService>();

// Register OrderMonitoringService with its dependencies
builder.Services.AddScoped<IOrderMonitoringService>(provider =>
{
    var orderApiClient = provider.GetRequiredService<ICoinbaseOrderApiClient>();
    var assembleOrderDetailsService = provider.GetRequiredService<IAssembleOrderDetailsService>();
    var pollingInterval = TimeSpan.FromSeconds(5); // Poll every 5 seconds
    var defaultTimeout = TimeSpan.FromMinutes(30); // Default timeout of 30 minutes
    
    return new OrderMonitoringService(
        orderApiClient,
        assembleOrderDetailsService,
        pollingInterval,
        defaultTimeout);
});

// Register services
builder.Services.AddScoped<IOrderValidationService, OrderValidationService>();
builder.Services.AddScoped<IProductInfoService, ProductInfoService>();

// Add controllers to the container
builder.Services.AddControllers();

var app = builder.Build();

// Development environment setup
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Map controllers to endpoints
app.MapControllers();

app.Run();