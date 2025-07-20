using crypto_bot_api.Data;
using crypto_bot_api.Services;
using Microsoft.EntityFrameworkCore;
using crypto_bot_api.Services.RateLimiting;
using crypto_bot_api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Enhanced logging for debugging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Load user secrets before adding services
builder.Configuration.AddUserSecrets<Program>();

// Configure sandbox mode
builder.Services.Configure<SandboxConfiguration>(
    builder.Configuration.GetSection(SandboxConfiguration.ConfigurationSection));
builder.Services.AddSandboxServices();

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Initialize product info
using (var scope = app.Services.CreateScope())
{
    var productInfoService = scope.ServiceProvider.GetRequiredService<IProductInfoService>();
    await productInfoService.InitializeAsync();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add sandbox middleware before authorization
app.UseSandboxHeaders();

app.UseAuthorization();
app.MapControllers();

app.Run();