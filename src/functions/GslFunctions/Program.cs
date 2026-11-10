using Gsl.Functions.Messaging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        // Opt-in Service Bus publisher. No-op unless ServiceBusConnection__fullyQualifiedNamespace is set.
        services.AddSingleton<IDisputeEventPublisher>(_ => ServiceBusDisputeEventPublisher.FromEnvironment());
    })
    .ConfigureLogging(logging =>
    {
        // The App Insights worker SDK adds a default Warning-only filter. Drop it so Information-level
        // RegEDisputeTracked entries reach App Insights traces.
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            var rule = options.Rules.FirstOrDefault(r =>
                r.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (rule is not null) options.Rules.Remove(rule);
        });
    })
    .Build();

host.Run();
