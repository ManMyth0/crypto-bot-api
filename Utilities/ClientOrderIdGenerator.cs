namespace crypto_bot_api.Utilities
{
    public static class ClientOrderIdGenerator
    {
        public static string GenerateCoinbaseClientOrderId()
        {
            // Create a random number generator
            var random = new Random();
            
            // Generate sections of the ID
            string section1 = random.Next(0, 10000).ToString().PadLeft(4, '0');
            string section2 = random.Next(0, 100000).ToString().PadLeft(5, '0');
            string section3 = random.Next(0, 1000000).ToString().PadLeft(6, '0');
            
            // Return formatted ID
            return $"{section1}-{section2}-{section3}";
        }
    }
} 