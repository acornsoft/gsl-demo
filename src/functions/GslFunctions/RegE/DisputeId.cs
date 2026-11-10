using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Gsl.Functions.RegE;

/// <summary>
/// Synthetic dispute ids for the fictional Gotham Savings and Loan demo: GSL-DSP-000123.
/// The id is the Service Bus correlationId, so one dispute can be followed across hops.
/// </summary>
public static partial class DisputeId
{
    public const string Prefix = "GSL-DSP-";

    [GeneratedRegex(@"^GSL-DSP-\d{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);

    /// <summary>A random synthetic id. Not tied to any real case.</summary>
    public static string NewSynthetic() =>
        Prefix + RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
}
