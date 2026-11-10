using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Gsl.Functions.RegE;

namespace Gsl.Functions.Messaging;

/// <summary>
/// Wire contract for "reg-e.deadlines.computed" messages on sbq-gsl-dev-001.
/// Schema id gsl.reg-e.deadlines/v1. Synthetic demo data only.
/// </summary>
public static class RegEDeadlinesMessage
{
    public const string Subject = "reg-e.deadlines.computed";
    public const string SchemaId = "gsl.reg-e.deadlines/v1";
    public const string ContentType = "application/json";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Body + stamped broker properties for one computed result.</summary>
    public static ServiceBusMessage Create(RegEDeadlines d, string disputeId, string? messageId = null)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (!DisputeId.IsValid(disputeId))
            throw new ArgumentException("disputeId must look like GSL-DSP-000123.", nameof(disputeId));

        var body = new
        {
            schema = SchemaId,
            synthetic = true,
            disputeId,
            noticeDate = d.NoticeDate,
            newAccount = d.NewAccount,
            posOrForeign = d.PosOrForeign,
            investigationBusinessDays = d.InvestigationBusinessDays,
            investigationDueDate = d.InvestigationDueDate,
            provisionalCreditDueDate = d.ProvisionalCreditDueDate,
            extendedInvestigationCalendarDays = d.ExtendedInvestigationCalendarDays,
            extendedInvestigationDueDate = d.ExtendedInvestigationDueDate,
            citations = d.Citations,
            note = d.Note,
        };

        var msg = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(body, Json))
        {
            MessageId = messageId ?? Guid.NewGuid().ToString("N"),
            CorrelationId = disputeId,
            Subject = Subject,
            ContentType = ContentType,
        };
        msg.ApplicationProperties["newAccount"] = d.NewAccount;
        msg.ApplicationProperties["posOrForeign"] = d.PosOrForeign;
        msg.ApplicationProperties["investigationDueDate"] = d.InvestigationDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        msg.ApplicationProperties["schema"] = SchemaId;
        msg.ApplicationProperties["synthetic"] = true;
        return msg;
    }
}

/// <summary>A message that passed the schema and consistency checks.</summary>
public sealed record TrackedDispute(
    string DisputeId,
    DateOnly NoticeDate,
    bool NewAccount,
    bool PosOrForeign,
    DateOnly InvestigationDueDate,
    DateOnly? ProvisionalCreditDueDate,
    DateOnly ExtendedInvestigationDueDate);

/// <summary>Track it, or dead-letter it with a reason the explorer can show.</summary>
public sealed record TrackingDecision(TrackedDispute? Dispute, string? DeadLetterReason, string? Description)
{
    public bool IsValid => Dispute is not null;
    public static TrackingDecision Track(TrackedDispute d) => new(d, null, null);
    public static TrackingDecision Reject(string reason, string description) => new(null, reason, description);
}

/// <summary>Dead-letter reasons. Short, stable codes: the explorer groups by them.</summary>
public static class DeadLetterReasons
{
    public const string BodyNotJson = "BodyNotJson";
    public const string SchemaViolation = "SchemaViolation";
    public const string MissingDisputeId = "MissingDisputeId";
    public const string InvalidDisputeId = "InvalidDisputeId";
    public const string DisputeIdMismatch = "DisputeIdMismatch";
    public const string InconsistentDeadlines = "InconsistentDeadlines";
}

/// <summary>
/// Validates a received body against gsl.reg-e.deadlines/v1. Pure and side-effect free so it is unit-tested
/// without a broker. Property names are matched case-insensitively so results sent before the
/// schema was stamped (PascalCase bodies) are judged on content, not casing.
/// </summary>
public static class RegEDeadlinesValidator
{
    public static TrackingDecision Validate(BinaryData? body, string? correlationId)
    {
        if (body is null || body.ToMemory().Length == 0)
            return TrackingDecision.Reject(DeadLetterReasons.BodyNotJson, "Message body is empty.");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(body.ToMemory()); }
        catch (JsonException ex)
        {
            return TrackingDecision.Reject(DeadLetterReasons.BodyNotJson, "Body is not valid JSON: " + Trim(ex.Message));
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return TrackingDecision.Reject(DeadLetterReasons.SchemaViolation, "Body must be a JSON object.");

            // Dispute id: body.disputeId and/or the correlationId. At least one, both well-formed, and equal.
            string? bodyId = null;
            if (TryGet(root, "disputeId", out var idEl) && idEl.ValueKind != JsonValueKind.Null)
            {
                if (idEl.ValueKind != JsonValueKind.String)
                    return TrackingDecision.Reject(DeadLetterReasons.InvalidDisputeId, "disputeId must be a string like GSL-DSP-000123.");
                bodyId = idEl.GetString();
            }
            var corr = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
            if (bodyId is null && corr is null)
                return TrackingDecision.Reject(DeadLetterReasons.MissingDisputeId,
                    "No dispute id: set correlationId (and body.disputeId) to a synthetic id like GSL-DSP-000123, then resubmit.");
            if (bodyId is not null && !DisputeId.IsValid(bodyId))
                return TrackingDecision.Reject(DeadLetterReasons.InvalidDisputeId, $"body.disputeId '{Trim(bodyId)}' does not match GSL-DSP-nnnnnn.");
            if (corr is not null && !DisputeId.IsValid(corr))
                return TrackingDecision.Reject(DeadLetterReasons.InvalidDisputeId, $"correlationId '{Trim(corr)}' does not match GSL-DSP-nnnnnn.");
            if (bodyId is not null && corr is not null && !string.Equals(bodyId, corr, StringComparison.Ordinal))
                return TrackingDecision.Reject(DeadLetterReasons.DisputeIdMismatch, $"body.disputeId {bodyId} differs from correlationId {corr}.");
            var disputeId = bodyId ?? corr!;

            var problems = new List<string>();
            var noticeDate = ReadDate(root, "noticeDate", problems, required: true);
            var newAccount = ReadBool(root, "newAccount", problems);
            var posOrForeign = ReadBool(root, "posOrForeign", problems);
            var businessDays = ReadInt(root, "investigationBusinessDays", problems);
            var investigationDue = ReadDate(root, "investigationDueDate", problems, required: true);
            var provisionalDue = ReadDate(root, "provisionalCreditDueDate", problems, required: false);
            var calendarDays = ReadInt(root, "extendedInvestigationCalendarDays", problems);
            var extendedDue = ReadDate(root, "extendedInvestigationDueDate", problems, required: true);
            if (problems.Count > 0)
                return TrackingDecision.Reject(DeadLetterReasons.SchemaViolation, string.Join("; ", problems));

            var expectedBusinessDays = newAccount ? 20 : 10;
            var expectedCalendarDays = newAccount || posOrForeign ? 90 : 45;
            var inconsistencies = new List<string>();
            if (businessDays != expectedBusinessDays)
                inconsistencies.Add($"investigationBusinessDays is {businessDays}, expected {expectedBusinessDays} (12 CFR 1005.11(c)(1)/(c)(3)(i))");
            if (calendarDays != expectedCalendarDays)
                inconsistencies.Add($"extendedInvestigationCalendarDays is {calendarDays}, expected {expectedCalendarDays} (12 CFR 1005.11(c)(3)(ii))");
            if (investigationDue!.Value <= noticeDate!.Value)
                inconsistencies.Add("investigationDueDate must be after noticeDate");
            if (extendedDue!.Value != noticeDate.Value.AddDays(calendarDays))
                inconsistencies.Add($"extendedInvestigationDueDate {extendedDue:yyyy-MM-dd} is not noticeDate + {calendarDays} days");
            if (inconsistencies.Count > 0)
                return TrackingDecision.Reject(DeadLetterReasons.InconsistentDeadlines, string.Join("; ", inconsistencies));

            return TrackingDecision.Track(new TrackedDispute(
                disputeId, noticeDate.Value, newAccount, posOrForeign, investigationDue.Value, provisionalDue, extendedDue.Value));
        }
    }

    private static DateOnly? ReadDate(JsonElement root, string name, List<string> problems, bool required)
    {
        if (!TryGet(root, name, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            if (required) problems.Add($"{name} is required");
            return null;
        }
        if (v.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(v.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        problems.Add($"{name} must be a yyyy-MM-dd date");
        return null;
    }

    private static bool ReadBool(JsonElement root, string name, List<string> problems)
    {
        if (TryGet(root, name, out var v))
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
        }
        problems.Add($"{name} is required and must be a boolean");
        return false;
    }

    private static int ReadInt(JsonElement root, string name, List<string> problems)
    {
        if (TryGet(root, name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i))
            return i;
        problems.Add($"{name} is required and must be an integer");
        return 0;
    }

    private static bool TryGet(JsonElement root, string name, out JsonElement value)
    {
        foreach (var p in root.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
        }
        value = default;
        return false;
    }

    // Dead-letter descriptions are capped by the broker (4 KB); keep them short and never echo a whole body.
    private static string Trim(string s) => s.Length <= 120 ? s : s[..120] + "...";
}
