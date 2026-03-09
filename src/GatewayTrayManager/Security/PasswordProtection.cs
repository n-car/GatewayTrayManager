using System;
using System.Security.Cryptography;
using System.Text;

namespace GatewayTrayManager.Security;

/// <summary>
/// Provides password encryption/decryption using Windows DPAPI.
/// Uses LocalMachine scope so all users on the same machine can decrypt.
/// </summary>
public static class PasswordProtection
{
    // Prefix to identify encrypted passwords in the config file
    private const string EncryptedPrefix = "DPAPI:";

    /// <summary>
    /// Encrypts a password using DPAPI (LocalMachine scope).
    /// All users on the same machine can decrypt this password.
    /// Returns a base64-encoded string prefixed with "DPAPI:".
    /// </summary>
    public static string? Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);
            var base64 = Convert.ToBase64String(encryptedBytes);
            return EncryptedPrefix + base64;
        }
        catch
        {
            // If encryption fails, return plain text (fallback)
            return plainText;
        }
    }

    /// <summary>
    /// Decrypts a DPAPI-encrypted password.
    /// If the password is not encrypted (no prefix), returns it as-is.
    /// </summary>
    public static string? Decrypt(string? encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return encryptedText;

        // Check if it's actually encrypted
        if (!encryptedText.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            // Not encrypted - return as-is (plain text password from old config)
            return encryptedText;
        }

        try
        {
            var base64 = encryptedText[EncryptedPrefix.Length..];
            var encryptedBytes = Convert.FromBase64String(base64);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // If decryption fails (different machine), return empty
            return null;
        }
    }

    /// <summary>
    /// Checks if a password string is encrypted (has DPAPI prefix).
    /// </summary>
    public static bool IsEncrypted(string? password)
    {
        return !string.IsNullOrEmpty(password) && password.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
    }
}
