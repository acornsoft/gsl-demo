using System.Net;
using System.Reflection;
using Gsl.Functions.Messaging;
using Gsl.Functions.RegE;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Gsl.Functions;

public sealed class HttpFunctions(IDisputeEventPublisher publisher, ILogger<HttpFunctions> log)
{
    private static readonly string Version =
        typeof(HttpFunctions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
        ?? "0.0.0";

    [Function("CheckHealth")]
    public async Task<HttpResponseData> CheckHealth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new { status = "ok", version = Version, timestamp = DateTimeOffset.UtcNow });
        return res;
    }

    [Function("CalculateRegEDeadlines")]
    public async Task<HttpResponseData> CalculateRegEDeadlines(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "disputes/reg-e/deadlines")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var (request, error) = RegERequestParser.Parse(body);
        if (request is null)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error });
            bad.StatusCode = HttpStatusCode.BadRequest; // WriteAsJsonAsync resets status to 200
            return bad;
        }

        var result = RegEDeadlineCalculator.Calculate(request);
        var disputeId = request.DisputeId ?? DisputeId.NewSynthetic();
        var publish = await publisher.PublishAsync(result, disputeId);
        log.LogInformation("Reg E deadlines calculated {DisputeId}; serviceBus={ServiceBus} messageId={MessageId}",
            disputeId, publish.Status, publish.MessageId);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteAsJsonAsync(new { disputeId, deadlines = result, serviceBus = publish.Status, messageId = publish.MessageId });
        return ok;
    }
}
