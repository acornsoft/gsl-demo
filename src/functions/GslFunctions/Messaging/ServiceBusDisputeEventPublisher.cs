using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace Gsl.Functions.Messaging;

public interface IDisputeEventPublisher
{
    /// <summary>Returns "sent", "skipped" (not configured) or "failed: ...". Never throws.</summary>
    Task<string> PublishAsync(object payload, CancellationToken ct = default);
}

/// <summary>
/// Opt-in publisher to the sbq-gsl-dev-001 queue using managed identity (no connection strings).
/// Enabled only when the app setting ServiceBusConnection__fullyQualifiedNamespace is present,
/// e.g. sbns-gsl-dev-eastus-001.servicebus.windows.net. Queue name from SERVICEBUS_QUEUE_NAME
/// (default sbq-gsl-dev-001). The identity needs "Azure Service Bus Data Sender" on the namespace.
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

    public async Task<string> PublishAsync(object payload, CancellationToken ct = default)
    {
        if (_sender is null) return "skipped";
        try
        {
            var msg = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(payload))
            {
                ContentType = "application/json",
                Subject = "gsl.regE.deadlinesCalculated",
            };
            await _sender.SendMessageAsync(msg, ct);
            return "sent";
        }
        catch (Exception ex)
        {
            return $"failed: {ex.GetType().Name}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender is not null) await _sender.DisposeAsync();
        if (_client is not null) await _client.DisposeAsync();
    }
}
