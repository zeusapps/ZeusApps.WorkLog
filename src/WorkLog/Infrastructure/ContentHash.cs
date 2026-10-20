using System.Security.Cryptography;
using System.Text;

namespace WorkLog.Infrastructure;

internal static class ContentHash
{
    public static string Compute(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
