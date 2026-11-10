using System.Collections.Concurrent;

namespace Gsl.Functions.Messaging;

/// <summary>
/// Records a key the first time it is seen. Used by the consumer to skip a message
/// that was already tracked (HTTP retry or broker redelivery).
/// </summary>
public interface ITrackedDisputeStore
{
    /// <summary>Returns true if this is the first time the key was seen and records it; false if already tracked.</summary>
    bool TryTrack(string key);
}

/// <summary>
/// In-memory dedupe store for the fictional GSL demo. Keys are Service Bus message ids
/// (falling back to the dispute id when messageId is missing).
/// Limits: process-local only. It is not shared across Flex Consumption instances and is
/// empty after a recycle. Service Bus Basic has no broker-side duplicate detection, so a
/// second instance can still track the same messageId once. Suitable for a single-instance
/// demo and for unit tests; not a production guarantee.
/// </summary>
public sealed class InMemoryTrackedDisputeStore : ITrackedDisputeStore
{
    private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.Ordinal);

    public bool TryTrack(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return true;
        return _seen.TryAdd(key, 0);
    }
}
