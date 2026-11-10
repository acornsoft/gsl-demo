using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Gsl.Functions.RegE;

namespace Gsl.Functions.Messaging;

public sealed record PublishResult(string Status, string? MessageId = null);

public interface IDisputeEventPublisher
{
    /// <summary>Status is "sent", "skipped" (not configured) or "failed: ...". Never throws.</summary>
    Task<PublishResult> PublishAsync(RegEDeadlines deadlines, string disputeId, string? messageId = null, CancellationToken ct = default);
}

/// <summary>
/// Opt-in publisher to the sbq-gsl-dev-001 queue using managed identity (no connection strings).
/// Enabled only when the app setting ServiceBusConnection__fullyQualifiedNamespace is present,
/// e.g. sbns-gsl-dev-eastus-001.servicebus.windows.net. Queue name from SERVICEBUS_QUEUE_NAME
/// (default sbq-gsl-dev-001). The identity needs "Azure Service Bus Data Sender" on the namespace.
/// Each message is stamped by <see cref="RegEDeadlinesMessage.Create"/>: messageId, correlationId = dispute id,
/// subject reg-e.deadlines.computed, and application properties.
/// </summary>
public sealed class ServiceBusDisputeEventPublisher : IDisputeEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient? _client;
    private readonly ServiceBusSender? _sender;

    private ServiceBusDisputeEventPublisher(string? fqns, string queue)
    {
        if (string.IsNullOrWhiteSpace(fqns)) return;
        _client = new ServiceBusClient(fqns, new DefaultAzureCredential());
        _sender = _client.CreateSender(queue);
    }

    public static ServiceBusDisputeEventPublisher FromEnvironment() => new(
        Environment.GetEnvironmentVariable("ServiceBusConnection__fullyQualifiedNamespace"),
        Environment.GetEnvironmentVariable("SERVICEBUS_QUEUE_NAME") ?? "sbq-gsl-dev-001");

    public bool Enabled => _sender is not null;

    public async Task<PublishResult> PublishAsync(RegEDeadlines deadlines, string disputeId, string? messageId = null, CancellationToken ct = default)
    {
        if (_sender is null) return new PublishResult("skipped");
        try
        {
            var msg = RegEDeadlinesMessage.Create(deadlines, disputeId, messageId);
            await _sender.SendMessageAsync(msg, ct);
            return new PublishResult("sent", msg.MessageId);
        }
        catch (Exception ex)
        {
            return new PublishResult($"failed: {ex.GetType().Name}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender is not null) await _sender.DisposeAsync();
        if (_client is not null) await _client.DisposeAsync();
    }
}
