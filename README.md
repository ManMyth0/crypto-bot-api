# Crypto Bot API

Currently an API service that integrates with the Coinbase Advanced Trade API to access cryptocurrency account information and perform trading operations.

## Features

- Position Management System
  - Track open and closed positions
  - Calculate P&L and commission tracking
  - Support for partial position closure
  - Filtered indexes for efficient position queries
- Product Information Management
  - Efficient caching with 24-hour refresh
  - Automatic startup initialization
  - Graceful handling of API failures
  - Real-time product status tracking
- Order Validation System
  - Comprehensive pre-trade validation
  - Trading status verification
  - Base size and minimum funds validation
  - Warning-based validation responses
- Structured responses using DTOs
- Retrieve account information from Coinbase
- Support for finding accounts with positive balances
- Properly formatted JWT claims for Coinbase compatibility
- Authentication with Coinbase Advanced Trade API using Ed25519 JWT signing

## Getting Started

### Prerequisites

- .NET 9.0 or higher
- Coinbase Advanced Trade API credentials (API Key ID and Secret)
- PostgreSQL database

### Configuration

The API uses user secrets for configuration. You'll need to set up the following secrets:

```json
{
  "PostgresLocalDatabaseConnection": "Host=localhost;Port=5432;Database=YOUR_POSTGRES_DATABASE;Username=postgres;Password=YOUR_POSTGRES_DATABASE_PASSWORD",
  "CoinbaseApi": {
    "baseUrl": "https://api.coinbase.com",
    "ApiKeyId": "YOUR_API_KEY_ID",
    "ApiSecret": "YOUR_API_SECRET",
    "Sandbox": {
      "Enabled": false // Explicitly set to false. Change to true for sandbox mode.
    }
  }
}
```

2. Replace `YOUR_POSTGRES_DATABASE` and `YOUR_POSTGRES_DATABASE_PASSWORD` with your Postgres credentials.
3. Replace `YOUR_API_KEY_ID` and `YOUR_API_SECRET` with your Coinbase API credentials.

### Sandbox Mode

The API supports Coinbase's sandbox environment for testing purposes. To enable sandbox mode:

3. No authentication is required in sandbox mode
1. Set `"Sandbox": { "Enabled": true }` in your secrets configuration
4. Sandbox provides standardized test responses for order creation and management
2. The API will automatically switch to use sandbox endpoints (`https://api-sandbox.coinbase.com`)

**Sandbox Limitations:**
- Individual order lookup is not supported
- Only Accounts and Orders endpoints are available
- Order management (edit, cancel) endpoints are limited
- Returns standardized test data rather than real account information

**Testing in Sandbox:**
- Market and limit orders work as expected
- Validation and error handling can be tested
- Historical order listing provides test data
- Order fills endpoint returns sample trade data

### Building and Running

```bash
dotnet restore  # Install dependency packages 
dotnet build    # Compile the project
dotnet run      # Run the application
```

The API will be available at `http://localhost:5294` or whatever your local host port number is.

## API Endpoints

### Account Endpoints
- `GET /api/CoinbaseAccount/config` - View the current configuration (for debugging)
- `GET /api/CoinbaseAccount/accounts` - Get all Coinbase accounts
- `GET /api/CoinbaseAccount/account-details` - Get details for an account with a positive balance
- `GET /api/CoinbaseAccount/account/{accountId}` - Get details for a specific account

### Order Endpoints
- `POST /api/CoinbaseOrder/orders` - Create an order with optional position tracking
- `GET /api/CoinbaseOrder/historical/fills` - Get historical fill information for orders with optional filtering

## Order Validation

The API performs comprehensive validation before submitting orders to Coinbase:

### Product Status Validation
- Checks if trading is disabled for the product
- Verifies product status (online/offline/delisted)
- Validates product-specific restrictions (limit-only, etc.)

### Order Size Validation
- Validates base size against product's base_increment
- Ensures order meets minimum funds requirement
- Checks quote size formatting and validity

### Validation Response
Instead of failing with errors, the API returns a ValidationResult containing:
```json
{
    "isValid": true,
    "warnings": [],
    "validationTimestamp": "2024-03-21T10:00:00Z",
    "productId": "BTC-USD",
    "status": "online",
    "tradingDisabled": false
}
```

Warnings are provided for:
- Trading disabled or offline products
- Delisted products
- Invalid base size increments
- Orders below minimum funds
- Limit-only product restrictions

### Position Tracking
The API requires position tracking information for proper trade management:
- `position_type`: Must be either "LONG" or "SHORT" (case-insensitive)
- Used to track position direction and calculate P&L
- Required for both opening and closing trades
- Works in conjunction with `position_id` for closing trades

## Order Request Format
```json
{
    "client_order_id": "optional-unique-id", // Auto-generated through the GenerateCoinbaseClientOrderId Utility
    "product_id": "BTC-USD",
    "side": "BUY",  // or "SELL"
    "position_type": "LONG",  // or "SHORT" - Required for position tracking, is case insensitive
    "position_id": "optional-uuid", // For closing trades. Omit for opening new positions
    "order_configuration": {
        "market_market_ioc": {
            // For market orders, only quote_size is required
            "quote_size": "20000"    // Required: Amount to spend in quote currency (e.g., USD)
        }
        // OR for limit orders (GTC - Good Till Canceled):
        "limit_limit_gtc": {
            "base_size": "0.001",    // Required: Amount in base currency (e.g., BTC)
            "quote_size": "20000",   // Required: Amount in quote currency (e.g., USD)
            "limit_price": "20000"   // Required: Price per unit
        }
        // OR for limit orders (GTD - Good Till Date):
        "limit_limit_gtd": {
            "base_size": "0.001",    // Required: Amount in base currency (e.g., BTC)
            "quote_size": "20000",   // Required: Amount in quote currency (e.g., USD)
            "limit_price": "20000",  // Required: Price per unit
            "end_time": "2024-12-31T23:59:59Z"  // Required: UTC timestamp when the order expires
        }
    }
}
```

## Database Schema

### Position Management Tables

- `Trade_Records` - Tracks overall position status and P&L
  - UUID-based position tracking
  - Filtered indexes for open positions
  - Decimal(18,8) precision for crypto values

- `Opening_Trades` - Initial position establishment
  - Links to Trade_Records
  - Tracks entry price and quantity

- `Closing_Trades` - Position closure tracking
  - Links to both Trade_Records and Opening_Trades
  - Supports partial position closure

### Product Information Table

- `ProductInfo` - Stores detailed configuration and trading parameters for each asset-pair available on Coinbase. 
  
  #### Which help us with:
  - Unique indexing on ProductId for fast lookups
  - Storing trading status and restrictions
  - Tracking base/quote increments and minimum funds
  - Including LastUpdated timestamp for cache management
  - Full decimal precision for currency-related fields
  - Maintaining product status and trading flags

Example asset-pair:
```json
{
    "id": "BTC-USD",
    "base_currency": "BTC",
    "quote_currency": "USD",
    "quote_increment": "0.01",
    "base_increment": "0.00000001",
    "display_name": "BTC-USD",
    "min_market_funds": "1",
    "margin_enabled": false,
    "post_only": false,
    "limit_only": false,
    "cancel_only": false,
    "status": "online",
    "status_message": "",
    "trading_disabled": false,
    "fx_stablecoin": false,
    "max_slippage_percentage": "0.02000000",
    "auction_mode": false,
    "high_bid_limit_percentage": ""
}
```

## Supported Order Types
- `limit_limit_gtd` - Good Till Date limit orders (GTD)
- `limit_limit_gtc` - Good Till Canceled limit orders (GTC)
- `market_market_ioc` - Immediate-or-Cancel market orders (IOC)

## Technologies Used

- C#
- .NET 9.0
- Testing Framework:
  - Moq (4.20.72) for mocking
  - Microsoft.NET.Test.Sdk (17.12.0)
  - MSTest.TestAdapter/Framework (3.6.4)
- Database:
  - PostgreSQL with Npgsql.EntityFrameworkCore.PostgreSQL (9.0.4)
  - Entity Framework Core (9.0.4) for:
    - Design (migrations)
    - InMemory (unit testing)
    - Sqlite (integration testing)
- API & Authentication:
  - Coinbase Advanced Trade API
  - Microsoft.IdentityModel.Tokens (8.9.0)
  - System.IdentityModel.Tokens.Jwt (8.9.0)
  - BouncyCastle.NetCore (2.2.1) for Ed25519 JWT signing
  - NSec.Cryptography (25.4.0) for cryptographic operations
- Documentation:
  - Swashbuckle.AspNetCore (9.0.3)
  - Microsoft.AspNetCore.OpenApi (9.0.4)