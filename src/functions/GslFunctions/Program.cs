using Gsl.Functions.Messaging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        // Opt-in Service Bus publisher. No-op unless ServiceBusConnection__fullyQualifiedNamespace is set.
        services.AddSingleton<IDisputeEventPublisher>(_ => ServiceBusDisputeEventPublisher.FromEnvironment());
    })
    .Build();

host.Run();
