using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Gsl.Functions.RegE;

namespace Gsl.Functions.Messaging;

/// <summary>
/// Deterministic Service Bus message ids for the GSL demo. Service Bus Basic has no
/// duplicate detection, so the publisher and the consumer both rely on this value.
/// When an <c>Idempotency-Key</c> header is present it is hashed alone; otherwise the
/// hash covers disputeId plus the request fields (noticeDate, newAccount, posOrForeign).
/// </summary>
public static class DeterministicMessageId
{
    public static string ForRequest(string? idempotencyKey, string disputeId, RegEDeadlineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ForRequest(idempotencyKey, disputeId, request.NoticeDate, request.NewAccount, request.PosOrForeign);
    }

    public static string ForRequest(string? idempotencyKey, string disputeId, DateOnly noticeDate, bool newAccount, bool posOrForeign)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            return Hash("v1\nidempotency-key\n" + idempotencyKey.Trim());

        var canonical = string.Create(CultureInfo.InvariantCulture,
            $"v1\ndisputeId={disputeId}\nnoticeDate={noticeDate:yyyy-MM-dd}\nnewAccount={newAccount}\nposOrForeign={posOrForeign}");
        return Hash(canonical);
    }

    private static string Hash(string canonical)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
