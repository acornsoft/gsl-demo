using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Gsl.Functions;
using Gsl.Functions.Messaging;
using Gsl.Functions.RegE;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Gsl.Functions.Tests;

public class DisputeIdTests
{
    // A valid 26-char ULID (first char 0-7, Crockford alphabet).
    private const string SampleUlid = "01J9H4X0ZQZ0ZQZ0ZQZ0ZQZ0ZQ";

    [Theory]
    [InlineData("GSL-DSP-000123", true)]   // legacy 6-digit form stays readable
    [InlineData("GSL-DSP-999999", true)]
    [InlineData("GSL-DSP-01J9H4X0ZQZ0ZQZ0ZQZ0ZQZ0ZQ", true)]
    [InlineData("GSL-DSP-12345", false)]
    [InlineData("GSL-DSP-1234567", false)]
    [InlineData("GSL-DSP-81J9H4X0ZQZ0ZQZ0ZQZ0ZQZ0ZQ", false)] // first char 8 is not a ULID
    [InlineData("GSL-DSP-01I9H4X0ZQZ0ZQZ0ZQZ0ZQZ0ZQ", false)] // I is not Crockford
    [InlineData("gsl-dsp-000123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Pattern(string? id, bool ok) => Assert.Equal(ok, DisputeId.IsValid(id));

    [Fact]
    public void NewSyntheticIsAValidUlid()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 50; i++)
        {
            var id = DisputeId.NewSynthetic();
            Assert.True(DisputeId.IsValid(id), id);
            Assert.StartsWith(DisputeId.Prefix, id);
            Assert.Equal(DisputeId.Prefix.Length + DisputeId.UlidLength, id.Length);
            Assert.True(seen.Add(id), "NewSynthetic should not collide in 50 draws");
        }
    }

    [Fact]
    public void EncodeUlid_KnownTimestampPrefix()
    {
        // 1469918176385 ms -> official ULID prefix 01ARYZ6S41
        var ms = 1469918176385L;
        var bytes = new byte[16];
        bytes[0] = (byte)(ms >> 40);
        bytes[1] = (byte)(ms >> 32);
        bytes[2] = (byte)(ms >> 24);
        bytes[3] = (byte)(ms >> 16);
        bytes[4] = (byte)(ms >> 8);
        bytes[5] = (byte)ms;
        var ulid = DisputeId.EncodeUlid(bytes);
        Assert.Equal(26, ulid.Length);
        Assert.Equal("01ARYZ6S41", ulid[..10]);
        Assert.True(DisputeId.IsValid(DisputeId.Prefix + ulid));
    }

    [Fact]
    public void ParserAcceptsLegacyAndUlidDisputeIds()
    {
        var (legacy, e1) = RegERequestParser.Parse("{\"noticeDate\":\"2026-11-09\",\"disputeId\":\"GSL-DSP-000123\"}");
        Assert.Null(e1);
        Assert.Equal("GSL-DSP-000123", legacy!.DisputeId);
        var (ulid, e2) = RegERequestParser.Parse("{\"noticeDate\":\"2026-11-09\",\"disputeId\":\"GSL-DSP-" + SampleUlid + "\"}");
        Assert.Null(e2);
        Assert.Equal("GSL-DSP-" + SampleUlid, ulid!.DisputeId);
    }

    [Fact]
    public void ParserAcceptsOptionalDisputeId()
    {
        var (req, err) = RegERequestParser.Parse("{\"noticeDate\":\"2026-11-09\",\"disputeId\":\"GSL-DSP-000123\"}");
        Assert.Null(err);
        Assert.Equal("GSL-DSP-000123", req!.DisputeId);
    }

    [Theory]
    [InlineData("{\"noticeDate\":\"2026-11-09\",\"disputeId\":\"CASE-1\"}")]
    [InlineData("{\"noticeDate\":\"2026-11-09\",\"disputeId\":123}")]
    public void ParserRejectsBadDisputeId(string body)
    {
        var (req, err) = RegERequestParser.Parse(body);
        Assert.Null(req);
        Assert.Contains("GSL-DSP-", err);
    }
}

public class MessageStampingTests
{
    private static RegEDeadlines Calc(bool newAccount, bool pos) =>
        RegEDeadlineCalculator.Calculate(new RegEDeadlineRequest(new DateOnly(2026, 11, 9), newAccount, pos));

    [Fact]
    public void StampsBrokerProperties()
    {
        var msg = RegEDeadlinesMessage.Create(Calc(true, false), "GSL-DSP-000123", "fixed-id");
        Assert.Equal("fixed-id", msg.MessageId);
        Assert.Equal("GSL-DSP-000123", msg.CorrelationId);
        Assert.Equal("reg-e.deadlines.computed", msg.Subject);
        Assert.Equal("application/json", msg.ContentType);
        Assert.Equal(true, msg.ApplicationProperties["newAccount"]);
        Assert.Equal(false, msg.ApplicationProperties["posOrForeign"]);
        Assert.Equal("2026-12-09", msg.ApplicationProperties["investigationDueDate"]);
        Assert.Equal("gsl.reg-e.deadlines/v1", msg.ApplicationProperties["schema"]);
        Assert.Equal(true, msg.ApplicationProperties["synthetic"]);
    }

    [Fact]
    public void GeneratesDeterministicMessageIdsFromRequestContent()
    {
        var a = RegEDeadlinesMessage.Create(Calc(false, false), "GSL-DSP-000001");
        var b = RegEDeadlinesMessage.Create(Calc(false, false), "GSL-DSP-000001");
        Assert.False(string.IsNullOrEmpty(a.MessageId));
        Assert.Equal(64, a.MessageId.Length);
        Assert.Equal(a.MessageId, b.MessageId);
        var otherDate = RegEDeadlineCalculator.Calculate(new RegEDeadlineRequest(new DateOnly(2026, 11, 10), false, false));
        var c = RegEDeadlinesMessage.Create(otherDate, "GSL-DSP-000001");
        Assert.NotEqual(a.MessageId, c.MessageId);
        var otherId = RegEDeadlinesMessage.Create(Calc(false, false), "GSL-DSP-000002");
        Assert.NotEqual(a.MessageId, otherId.MessageId);
    }

    [Fact]
    public void IdempotencyKeyOverridesRequestContent()
    {
        var same = DeterministicMessageId.ForRequest("retry-1", "GSL-DSP-000001", new DateOnly(2026, 11, 9), false, false);
        var otherContent = DeterministicMessageId.ForRequest("retry-1", "GSL-DSP-000999", new DateOnly(2026, 12, 1), true, true);
        var otherKey = DeterministicMessageId.ForRequest("retry-2", "GSL-DSP-000001", new DateOnly(2026, 11, 9), false, false);
        var trimmed = DeterministicMessageId.ForRequest("  retry-1  ", "GSL-DSP-000001", new DateOnly(2026, 11, 9), false, false);
        var fromContent = DeterministicMessageId.ForRequest(null, "GSL-DSP-000001", new DateOnly(2026, 11, 9), false, false);
        Assert.Equal(same, otherContent);
        Assert.Equal(same, trimmed);
        Assert.NotEqual(same, otherKey);
        Assert.NotEqual(same, fromContent);
        Assert.Equal(64, same.Length);
    }

    [Fact]
    public void BodyIsCamelCaseAndCarriesDisputeId()
    {
        var msg = RegEDeadlinesMessage.Create(Calc(false, true), "GSL-DSP-000777");
        using var doc = JsonDocument.Parse(msg.Body.ToMemory());
        var root = doc.RootElement;
        Assert.Equal("GSL-DSP-000777", root.GetProperty("disputeId").GetString());
        Assert.Equal("gsl.reg-e.deadlines/v1", root.GetProperty("schema").GetString());
        Assert.True(root.GetProperty("synthetic").GetBoolean());
        Assert.Equal("2026-11-09", root.GetProperty("noticeDate").GetString());
        Assert.Equal(90, root.GetProperty("extendedInvestigationCalendarDays").GetInt32());
    }

    [Fact]
    public void RejectsBadDisputeId() =>
        Assert.Throws<ArgumentException>(() => RegEDeadlinesMessage.Create(Calc(false, false), "not-an-id"));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void StampedMessagesRoundTripThroughTheValidator(bool newAccount, bool pos)
    {
        var msg = RegEDeadlinesMessage.Create(Calc(newAccount, pos), "GSL-DSP-000123");
        var decision = RegEDeadlinesValidator.Validate(msg.Body, msg.CorrelationId);
        Assert.True(decision.IsValid, decision.Description);
        Assert.Equal("GSL-DSP-000123", decision.Dispute!.DisputeId);
    }
}

public class ValidatorTests
{
    private const string Good =
        "{\"disputeId\":\"GSL-DSP-000123\",\"noticeDate\":\"2026-11-09\",\"newAccount\":false,\"posOrForeign\":false," +
        "\"investigationBusinessDays\":10,\"investigationDueDate\":\"2026-11-24\",\"provisionalCreditDueDate\":\"2026-11-24\"," +
        "\"extendedInvestigationCalendarDays\":45,\"extendedInvestigationDueDate\":\"2026-12-24\"}";

    private static TrackingDecision V(string body, string? corr = "GSL-DSP-000123") =>
        RegEDeadlinesValidator.Validate(BinaryData.FromString(body), corr);

    private static string With(string key, string jsonValue)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(Good)!.AsObject();
        if (jsonValue == "<remove>") node.Remove(key); else node[key] = System.Text.Json.Nodes.JsonNode.Parse(jsonValue);
        return node.ToJsonString();
    }

    [Fact]
    public void ValidMessageIsTracked()
    {
        var d = V(Good);
        Assert.True(d.IsValid);
        Assert.Equal(new DateOnly(2026, 11, 24), d.Dispute!.InvestigationDueDate);
        Assert.Equal(new DateOnly(2026, 12, 24), d.Dispute.ExtendedInvestigationDueDate);
    }

    [Fact]
    public void CorrelationIdAloneIsEnough() => Assert.True(V(With("disputeId", "<remove>")).IsValid);

    [Fact]
    public void BodyIdAloneIsEnough() => Assert.True(V(Good, corr: null).IsValid);

    [Fact]
    public void PascalCaseBodiesAreAccepted()
    {
        var pascal = Good.Replace("\"noticeDate\"", "\"NoticeDate\"").Replace("\"newAccount\"", "\"NewAccount\"");
        Assert.True(V(pascal).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json {")]
    public void NonJsonIsDeadLettered(string body) =>
        Assert.Equal(DeadLetterReasons.BodyNotJson, V(body).DeadLetterReason);

    [Fact]
    public void JsonArrayIsSchemaViolation() =>
        Assert.Equal(DeadLetterReasons.SchemaViolation, V("[1,2]").DeadLetterReason);

    [Fact]
    public void MissingIdEverywhere()
    {
        var d = V(With("disputeId", "<remove>"), corr: null);
        Assert.Equal(DeadLetterReasons.MissingDisputeId, d.DeadLetterReason);
        Assert.Contains("resubmit", d.Description);
    }

    [Fact]
    public void MalformedIds()
    {
        Assert.Equal(DeadLetterReasons.InvalidDisputeId, V(With("disputeId", "\"CASE-9\"")).DeadLetterReason);
        Assert.Equal(DeadLetterReasons.InvalidDisputeId, V(With("disputeId", "42")).DeadLetterReason);
        Assert.Equal(DeadLetterReasons.InvalidDisputeId, V(With("disputeId", "<remove>"), corr: "abc").DeadLetterReason);
    }

    [Fact]
    public void IdMismatch() =>
        Assert.Equal(DeadLetterReasons.DisputeIdMismatch, V(Good, corr: "GSL-DSP-000999").DeadLetterReason);

    [Theory]
    [InlineData("noticeDate", "<remove>", "noticeDate is required")]
    [InlineData("noticeDate", "\"11/09/2026\"", "noticeDate must be a yyyy-MM-dd date")]
    [InlineData("newAccount", "\"yes\"", "newAccount is required and must be a boolean")]
    [InlineData("investigationBusinessDays", "\"ten\"", "investigationBusinessDays is required")]
    [InlineData("extendedInvestigationDueDate", "<remove>", "extendedInvestigationDueDate is required")]
    public void SchemaViolations(string key, string value, string expected)
    {
        var d = V(With(key, value));
        Assert.Equal(DeadLetterReasons.SchemaViolation, d.DeadLetterReason);
        Assert.Contains(expected, d.Description);
    }

    [Theory]
    [InlineData("investigationBusinessDays", "20", "expected 10")]
    [InlineData("extendedInvestigationCalendarDays", "90", "expected 45")]
    [InlineData("investigationDueDate", "\"2026-11-01\"", "after noticeDate")]
    [InlineData("extendedInvestigationDueDate", "\"2026-12-25\"", "noticeDate + 45")]
    public void InconsistentDeadlines(string key, string value, string expected)
    {
        var d = V(With(key, value));
        Assert.Equal(DeadLetterReasons.InconsistentDeadlines, d.DeadLetterReason);
        Assert.Contains(expected, d.Description);
    }

    [Fact]
    public void DescriptionsDoNotEchoLongValues()
    {
        var d = V(With("disputeId", "\"" + new string('x', 500) + "\""));
        Assert.True(d.Description!.Length < 250);
    }
}

public class ConsumerFunctionTests
{
    private sealed class FakeActions : ServiceBusMessageActions
    {
        public List<string> Calls { get; } = [];
        public string? Reason { get; private set; }
        public string? Description { get; private set; }

        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        {
            Calls.Add("complete");
            return Task.CompletedTask;
        }

        public override Task DeadLetterMessageAsync(
            ServiceBusReceivedMessage message,
            Dictionary<string, object>? propertiesToModify = null,
            string? deadLetterReason = null,
            string? deadLetterErrorDescription = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("deadletter");
            Reason = deadLetterReason;
            Description = deadLetterErrorDescription;
            return Task.CompletedTask;
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, EventId Id, string Text)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, eventId, formatter(state, exception)));
    }

    private static ServiceBusReceivedMessage Received(BinaryData body, string? correlationId, string messageId = "m-1", int deliveryCount = 1) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: messageId, correlationId: correlationId, deliveryCount: deliveryCount);

    private static RegEDisputeConsumer Consumer(ListLogger<RegEDisputeConsumer> log, ITrackedDisputeStore? store = null) =>
        new(log, store ?? new InMemoryTrackedDisputeStore());

    [Fact]
    public async Task ValidMessageIsLoggedAndCompleted()
    {
        var stamped = RegEDeadlinesMessage.Create(
            RegEDeadlineCalculator.Calculate(new RegEDeadlineRequest(new DateOnly(2026, 11, 9), false, false)), "GSL-DSP-000123");
        var log = new ListLogger<RegEDisputeConsumer>();
        var actions = new FakeActions();

        await Consumer(log).Run(Received(stamped.Body, stamped.CorrelationId, stamped.MessageId), actions, CancellationToken.None);

        Assert.Equal(["complete"], actions.Calls);
        var entry = Assert.Single(log.Entries);
        Assert.Equal("RegEDisputeTracked", entry.Id.Name);
        Assert.StartsWith("RegEDisputeTracked GSL-DSP-000123", entry.Text);
        Assert.Contains("investigationDueDate=2026-11-24", entry.Text);
        Assert.Contains("extendedInvestigationDueDate=2026-12-24", entry.Text);
    }

    [Fact]
    public async Task InvalidMessageIsDeadLetteredWithReason()
    {
        var log = new ListLogger<RegEDisputeConsumer>();
        var actions = new FakeActions();

        await Consumer(log).Run(Received(BinaryData.FromString("{\"synthetic\":true}"), null), actions, CancellationToken.None);

        Assert.Equal(["deadletter"], actions.Calls);
        Assert.Equal(DeadLetterReasons.MissingDisputeId, actions.Reason);
        Assert.False(string.IsNullOrWhiteSpace(actions.Description));
        Assert.Equal("RegEDisputeDeadLettered", Assert.Single(log.Entries).Id.Name);
    }

    [Fact]
    public async Task GarbageIsDeadLetteredNotThrown()
    {
        var actions = new FakeActions();
        await Consumer(new ListLogger<RegEDisputeConsumer>())
            .Run(Received(BinaryData.FromString("\u0000\u0001garbage"), "GSL-DSP-000123"), actions, CancellationToken.None);
        Assert.Equal(DeadLetterReasons.BodyNotJson, actions.Reason);
    }

    [Fact]
    public async Task DuplicateMessageIdIsCompletedAndNotTrackedAgain()
    {
        var stamped = RegEDeadlinesMessage.Create(
            RegEDeadlineCalculator.Calculate(new RegEDeadlineRequest(new DateOnly(2026, 11, 9), false, false)), "GSL-DSP-000123");
        var store = new InMemoryTrackedDisputeStore();
        var log = new ListLogger<RegEDisputeConsumer>();
        var first = new FakeActions();
        var second = new FakeActions();

        await Consumer(log, store).Run(Received(stamped.Body, stamped.CorrelationId, stamped.MessageId), first, CancellationToken.None);
        await Consumer(log, store).Run(Received(stamped.Body, stamped.CorrelationId, stamped.MessageId, deliveryCount: 2), second, CancellationToken.None);

        Assert.Equal(["complete"], first.Calls);
        Assert.Equal(["complete"], second.Calls);
        Assert.Equal("RegEDisputeTracked", log.Entries[0].Id.Name);
        Assert.Equal("RegEDisputeDuplicateSkipped", log.Entries[1].Id.Name);
        Assert.Contains("GSL-DSP-000123", log.Entries[1].Text);
        Assert.DoesNotContain("investigationDueDate=", log.Entries[1].Text);
    }

    [Fact]
    public async Task DifferentMessageIdsAreBothTracked()
    {
        var deadlines = RegEDeadlineCalculator.Calculate(new RegEDeadlineRequest(new DateOnly(2026, 11, 9), false, false));
        var a = RegEDeadlinesMessage.Create(deadlines, "GSL-DSP-000123", "id-a");
        var b = RegEDeadlinesMessage.Create(deadlines, "GSL-DSP-000123", "id-b");
        var store = new InMemoryTrackedDisputeStore();
        var log = new ListLogger<RegEDisputeConsumer>();

        await Consumer(log, store).Run(Received(a.Body, a.CorrelationId, a.MessageId), new FakeActions(), CancellationToken.None);
        await Consumer(log, store).Run(Received(b.Body, b.CorrelationId, b.MessageId), new FakeActions(), CancellationToken.None);

        Assert.Equal(2, log.Entries.Count);
        Assert.All(log.Entries, e => Assert.Equal("RegEDisputeTracked", e.Id.Name));
    }

    [Fact]
    public void TriggerUsesIdentityConnectionAndManualSettlement()
    {
        var method = typeof(RegEDisputeConsumer).GetMethod(nameof(RegEDisputeConsumer.Run))!;
        var attr = method.GetParameters()[0].GetCustomAttributes(typeof(ServiceBusTriggerAttribute), false)
            .Cast<ServiceBusTriggerAttribute>().Single();
        Assert.Equal("ServiceBusConnection", attr.Connection);
        Assert.False(attr.AutoCompleteMessages);
        Assert.Equal("%SERVICEBUS_QUEUE_NAME%", attr.QueueName);
    }
}

public class FunctionNameTests
{
    [Fact]
    public void HttpFunctionsUseAgreedNames()
    {
        var health = typeof(HttpFunctions).GetMethod(nameof(HttpFunctions.CheckHealth))!
            .GetCustomAttributes(typeof(FunctionAttribute), false).Cast<FunctionAttribute>().Single();
        var calc = typeof(HttpFunctions).GetMethod(nameof(HttpFunctions.CalculateRegEDeadlines))!
            .GetCustomAttributes(typeof(FunctionAttribute), false).Cast<FunctionAttribute>().Single();
        Assert.Equal("CheckHealth", health.Name);
        Assert.Equal("CalculateRegEDeadlines", calc.Name);
    }

    [Fact]
    public void ConsumerNameIsUnchanged()
    {
        var attr = typeof(RegEDisputeConsumer).GetMethod(nameof(RegEDisputeConsumer.Run))!
            .GetCustomAttributes(typeof(FunctionAttribute), false).Cast<FunctionAttribute>().Single();
        Assert.Equal("regEDisputeConsumer", attr.Name);
    }
}

