using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureEventHubConsumerClient("event-hubs");
#if !SKIP_UNSTABLE_EMULATORS
builder.AddAzureServiceBusClient("messaging");
#endif

builder.ConfigureFunctionsWebApplication();

// builder.AddAzureEventHubConsumerClient("event-hubs");
// builder.AddAzureEventHubProducerClient("event-hubs");

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();