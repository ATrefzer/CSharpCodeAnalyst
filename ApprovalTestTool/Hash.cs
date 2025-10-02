using System.Security.Cryptography;
using System.Text;

namespace ApprovalTestTool;

/// <summary>
public static class Hash
{
    public static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(inputBytes);

        // Format as hex string
        var hashString = new StringBuilder();
        foreach (var b in hashBytes)
        {
            hashString.Append(b.ToString("x2"));
        }

        return hashString.ToString();
    }
}