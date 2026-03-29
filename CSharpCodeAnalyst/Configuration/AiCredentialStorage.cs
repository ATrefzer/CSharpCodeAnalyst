using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CSharpCodeAnalyst.Configuration;

/// <summary>
///     Stores the AI API key encrypted with DPAPI (Windows Data Protection API).
///     Encryption is scoped to the current user — no other user can decrypt it.
/// </summary>
public static class AiCredentialStorage
{
    private static readonly string CredentialFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CSharpCodeAnalyst", "ai_key.dat");

    public static void SaveApiKey(string apiKey)
    {
        var data = Encoding.UTF8.GetBytes(apiKey);
        var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(Path.GetDirectoryName(CredentialFile)!);
        File.WriteAllBytes(CredentialFile, encrypted);
    }

    public static string LoadApiKey()
    {
        if (!File.Exists(CredentialFile))
        {
            return string.Empty;
        }

        try
        {
            var encrypted = File.ReadAllBytes(CredentialFile);
            var data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static bool HasApiKey()
    {
        return File.Exists(CredentialFile);
    }
}
