using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Gsl.Functions.RegE;

/// <summary>
/// Synthetic dispute ids for the fictional Gotham Savings and Loan demo.
/// New ids are <c>GSL-DSP-</c> plus a 26-character ULID (Crockford base32).
/// Legacy <c>GSL-DSP-nnnnnn</c> ids stay valid so existing messages can be read.
/// The id is the Service Bus correlationId, so one dispute can be followed across hops.
/// </summary>
public static partial class DisputeId
{
    public const string Prefix = "GSL-DSP-";
    public const int UlidLength = 26;

    // 6-digit legacy form, or a ULID: first char is 0-7 (48-bit timestamp encoding), rest is Crockford base32.
    [GeneratedRegex(@"^GSL-DSP-(\d{6}|[0-7][0-9A-HJKMNP-TV-Z]{25})$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);

    /// <summary>A new synthetic id: GSL-DSP- plus a ULID. Not tied to any real case.</summary>
    public static string NewSynthetic() => Prefix + NewUlid();

    /// <summary>26-character Crockford ULID (timestamp + randomness). Uppercase, no I/L/O/U.</summary>
    internal static string NewUlid()
    {
        Span<byte> bytes = stackalloc byte[16];
        var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(ms >> 40);
        bytes[1] = (byte)(ms >> 32);
        bytes[2] = (byte)(ms >> 24);
        bytes[3] = (byte)(ms >> 16);
        bytes[4] = (byte)(ms >> 8);
        bytes[5] = (byte)ms;
        RandomNumberGenerator.Fill(bytes[6..]);
        return EncodeUlid(bytes);
    }

    internal static string EncodeUlid(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 16) throw new ArgumentException("A ULID is 16 bytes.", nameof(bytes));
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        Span<char> chars = stackalloc char[UlidLength];
        // 2 leading zero bits so 128 bits encode as 26 Crockford characters (130 bits).
        var bitBuffer = 0;
        var bitCount = 2;
        var dest = 0;
        foreach (var b in bytes)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;
            while (bitCount >= 5)
            {
                bitCount -= 5;
                chars[dest++] = alphabet[(bitBuffer >> bitCount) & 31];
            }
        }
        return new string(chars);
    }
}
