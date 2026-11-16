using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using TKS.LeadPilot.Core.Interfaces;

namespace TKS.LeadPilot.UI.Services;

[SupportedOSPlatform("windows")]
public class DpapiSecretVault : ISecretVault
{
    private readonly string _vaultDirectory;

    public DpapiSecretVault(string? customVaultPath = null)
    {
        _vaultDirectory = customVaultPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TKS.LeadPilot",
            "Vault");

        if (!Directory.Exists(_vaultDirectory))
        {
            Directory.CreateDirectory(_vaultDirectory);
        }
    }

    public void SaveSecret(string key, string secret)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        var filePath = GetSecretFilePath(key);
        if (string.IsNullOrEmpty(secret))
        {
            if (File.Exists(filePath)) File.Delete(filePath);
            return;
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(secret);
        var encryptedBytes = ProtectedData.Protect(plaintextBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(filePath, encryptedBytes);
    }

    public string? GetSecret(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        var filePath = GetSecretFilePath(key);
        if (!File.Exists(filePath)) return null;

        try
        {
            var encryptedBytes = File.ReadAllBytes(filePath);
            var plaintextBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch
        {
            return null;
        }
    }

    public void DeleteSecret(string key)
    {
        var filePath = GetSecretFilePath(key);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public bool HasSecret(string key)
    {
        var filePath = GetSecretFilePath(key);
        return File.Exists(filePath);
    }

    private string GetSecretFilePath(string key)
    {
        // Sanitize key for filesystem
        var safeKey = string.Concat(key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_vaultDirectory, $"{safeKey}.sec");
    }
}
