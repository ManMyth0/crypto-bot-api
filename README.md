# Crypto Bot API

Currently an API service that integrates with the Coinbase Advanced Trade API to access cryptocurrency account information and perform trading operations.

## Features

- Structured responses using DTOs
- Retrieve account information from Coinbase
- Support for finding accounts with positive balances
- Properly formatted JWT claims for Coinbase compatibility
- Authentication with Coinbase Advanced Trade API using Ed25519 JWT signing

## Getting Started

### Prerequisites

- .NET 9.0 or higher
- Coinbase Advanced Trade API credentials (API Key ID and Secret)

### Configuration

1. Create an `appsettings.json` file with the following structure:

```json
{
  "ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=YOUR_POSTGRES_DATABASE;Username=postgres;Password=YOUR_POSTGRES_DATABASE_PASSWORD"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "CoinbaseApi": {
    "baseUrl": "https://api.coinbase.com",
    "ApiKeyId": "YOUR_API_KEY_ID",
    "ApiSecret": "YOUR_API_SECRET"
  }
}
```

2. Replace `YOUR_POSTGRES_DATABASE` and `YOUR_POSTGRES_DATABASE_PASSWORD` with your Postgres credentials.
3. Replace `YOUR_API_KEY_ID` and `YOUR_API_SECRET` with your Coinbase API credentials.

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
- `POST /api/CoinbaseOrder/orders` - Create an order (buy or sell) by specifying the "side" property in the request body
- `GET /api/CoinbaseOrder/historical/fills` - Get historical fill information for orders with optional filtering

## Order Request Format
```json
{
    "product_id": "BTC-USD",
    "side": "BUY",  // or "SELL"
    "client_order_id": "optional-unique-id", // Auto-generated through the GenerateCoinbaseClientOrderId Utility
    "order_configuration": {
        "limit_limit_gtc": {
            "base_size": "0.001",
            "limit_price": "20000",
            "post_only": false
        }
    }
}
```

## Supported Order Types
- `limit_limit_gtc` - Good Till Canceled limit orders (GTC)
- `limit_limit_gtd` - Good Till Date limit orders (GTD)

## Technologies Used

- C#
- .NET 9.0
- MSTest for unit testing
- Moq for mocking in tests
- Coinbase Advanced Trade API
- PostgreSQL with Entity Framework Core 9.0.4
- System.Text.Json for JSON serialization/deserialization
- NSec.Cryptography (25.4.0) for cryptographic operations
- System.IdentityModel.Tokens.Jwt (8.9.0) for JWT handling
- Microsoft.IdentityModel.Tokens (8.9.0) for token management
- BouncyCastle.NetCore (2.2.1) for Ed25519 cryptographic signing