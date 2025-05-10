namespace crypto_bot_api.CustomExceptions
{
    public class CoinbaseApiException : Exception
    {
        public CoinbaseApiException() { }

        public CoinbaseApiException(string message)
            : base(message) { }

        public CoinbaseApiException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
