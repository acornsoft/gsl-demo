using Azure.Messaging.ServiceBus;
using Gsl.Functions.Messaging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Gsl.Functions;

/// <summary>
/// Queue-triggered consumer for sbq-gsl-dev-001 using the identity-based connection "ServiceBusConnection"
/// (ServiceBusConnection__fullyQualifiedNamespace + __credential=managedidentity). Settles explicitly:
/// valid -> log "RegEDisputeTracked" and complete; invalid -> dead-letter with a reason and description.
/// An unexpected exception is not caught: the lock lapses, the broker redelivers, and after
/// maxDeliveryCount (10) the broker dead-letters it as MaxDeliveryCountExceeded.
/// </summary>
public sealed class RegEDisputeConsumer(ILogger<RegEDisputeConsumer> log)
{
    public static readonly EventId TrackedEvent = new(4100, "RegEDisputeTracked");
    public static readonly EventId DeadLetteredEvent = new(4101, "RegEDisputeDeadLettered");

    [Function("regEDisputeConsumer")]
    public async Task Run(
        [ServiceBusTrigger("%SERVICEBUS_QUEUE_NAME%", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken ct)
    {
        var decision = RegEDeadlinesValidator.Validate(message.Body, message.CorrelationId);
        if (decision.Dispute is { } d)
        {
            log.LogInformation(TrackedEvent,
                "RegEDisputeTracked {DisputeId} noticeDate={NoticeDate} investigationDueDate={InvestigationDueDate} provisionalCreditDueDate={ProvisionalCreditDueDate} extendedInvestigationDueDate={ExtendedInvestigationDueDate} newAccount={NewAccount} posOrForeign={PosOrForeign} messageId={MessageId} deliveryCount={DeliveryCount}",
                d.DisputeId, d.NoticeDate.ToString("yyyy-MM-dd"), d.InvestigationDueDate.ToString("yyyy-MM-dd"),
                d.ProvisionalCreditDueDate?.ToString("yyyy-MM-dd"), d.ExtendedInvestigationDueDate.ToString("yyyy-MM-dd"),
                d.NewAccount, d.PosOrForeign, message.MessageId, message.DeliveryCount);
            await actions.CompleteMessageAsync(message, ct);
            return;
        }

        log.LogWarning(DeadLetteredEvent,
            "RegEDisputeDeadLettered reason={DeadLetterReason} description={DeadLetterDescription} messageId={MessageId} correlationId={CorrelationId}",
            decision.DeadLetterReason, decision.Description, message.MessageId, message.CorrelationId);
        await actions.DeadLetterMessageAsync(
            message,
            deadLetterReason: decision.DeadLetterReason!,
            deadLetterErrorDescription: decision.Description,
            cancellationToken: ct);
    }
}
