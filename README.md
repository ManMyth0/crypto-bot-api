# Crypto Bot API

An API service that integrates with the Coinbase Advanced Trade API to access cryptocurrency account information and perform trading operations.

## Features

- Authentication with Coinbase Advanced Trade API using Ed25519 JWT signing
- Retrieve account information from Coinbase
- Support for finding accounts with positive balances
- Properly formatted JWT claims for Coinbase compatibility

## Getting Started

### Prerequisites

- .NET 9.0 or higher
- Coinbase Advanced Trade API credentials (API Key ID and Secret)

### Configuration

1. Create an `appsettings.json` file with the following structure:

```json
{
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

2. Replace `YOUR_API_KEY_ID` and `YOUR_API_SECRET` with your Coinbase API credentials.

### Building and Running

```bash
dotnet build
dotnet run
```

The API will be available at `http://localhost:5294`.

## API Endpoints

- `GET /api/CoinbaseAccount/accounts` - Get all Coinbase accounts
- `GET /api/CoinbaseAccount/account-details` - Get details for an account with a positive balance
- `GET /api/CoinbaseAccount/account/{accountId}` - Get details for a specific account
- `GET /api/CoinbaseAccount/config` - View the current configuration (for debugging)

## Technologies Used

- .NET 9.0
- C#
- BouncyCastle (for Ed25519 signing)
- Coinbase Advanced Trade API 