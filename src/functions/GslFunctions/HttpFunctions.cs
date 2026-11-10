using System.Net;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Gsl.Functions;

// Minimal placeholder for func-gsl-dev-eastus-001 (.NET 8 isolated).
// Ted will replace this with the Reg E app from feat/fn-reg-e-clock.
public sealed class HttpFunctions
{
    [Function("health")]
    public async Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new
        {
            status = "ok",
            service = "gsl-demo",
            time = DateTimeOffset.UtcNow
        });
        return res;
    }

    [Function("queue-check")]
    public async Task<HttpResponseData> QueueCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "queue-check")] HttpRequestData req)
    {
        var fqdn = Environment.GetEnvironmentVariable("ServiceBusConnection__fullyQualifiedNamespace");
        var queue = Environment.GetEnvironmentVariable("SERVICEBUS_QUEUE_NAME") ?? "sbq-gsl-dev-001";
        if (string.IsNullOrWhiteSpace(fqdn))
        {
            var bad = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await bad.WriteAsJsonAsync(new { status = "error", error = "Service Bus namespace setting is missing." });
            bad.StatusCode = HttpStatusCode.ServiceUnavailable;
            return bad;
        }

        await using var client = new ServiceBusClient(fqdn, new DefaultAzureCredential());
        await using var receiver = client.CreateReceiver(queue);
        // Peek only. An empty queue still proves the identity can reach the queue. No messages are sent.
        _ = await receiver.PeekMessageAsync();

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteAsJsonAsync(new
        {
            status = "ok",
            service = "gsl-demo",
            queue,
            reachable = true
        });
        return ok;
    }
}
