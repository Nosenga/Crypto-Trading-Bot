namespace TradingBot.Tools
{
    /// <summary>
    /// Generates and verifies BCrypt hashes for the admin password.
    /// </summary>
    public static class PasswordHasher
    {
        public static string Hash(string plaintext) =>
            BCrypt.Net.BCrypt.HashPassword(plaintext, workFactor: 12);

        public static bool Verify(string plaintext, string hash)
        {
            if (string.IsNullOrWhiteSpace(hash)) return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(plaintext, hash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }
    }
}