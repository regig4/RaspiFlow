using Microsoft.Extensions.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// Adding event hub emulator
var eventHubs = builder.AddAzureEventHubs("event-hubs")
                       .RunAsEmulator(c => c.WithHostPort(7776))
                       .WithExternalHttpEndpoints();

var eh = eventHubs.AddHub("eh1");

// Adding Service bus emulator
var serviceBus = builder
    .AddAzureServiceBus("myservicebus")
    .RunAsEmulator(c => c
        .WithLifetime(ContainerLifetime.Persistent)
        .WithHostPort(7777))
    .WithExternalHttpEndpoints();

serviceBus.AddServiceBusQueue("myqueue");


// Adding python script which reads signals from RaspberryPi
#pragma warning disable ASPIREHOSTINGPYTHON001

var pythonapp = builder.AddPythonApp("start", "../PythonClient", "start.py")
       .WithHttpEndpoint(env: "PORT")
       .WithExternalHttpEndpoints()
       .WithOtlpExporter();
	
#pragma warning restore ASPIREHOSTINGPYTHON001

if (builder.ExecutionContext.IsRunMode && builder.Environment.IsDevelopment())
{
    pythonapp.WithEnvironment("DEBUG", "True");
}

pythonapp.WithReference(eventHubs);
pythonapp.WithReference(serviceBus);

// Adding Azure functions project

var functions = builder.AddAzureFunctionsProject<Projects.RaspberryAzure_PersistanceWorker>("functions")
                       .WithExternalHttpEndpoints();

functions.WithReference(eventHubs);
functions.WithReference(serviceBus).WaitFor(serviceBus);

var aggregator = builder.AddProject<RaspberryAzure_AggregatorService>("aggregator");

aggregator.WithReference(serviceBus).WaitFor(serviceBus);

// var ollama = builder.AddContainer("ollama", "kcsurapaneni/ollama-phi3")
//     .WithHttpEndpoint(port: 11434, targetPort: 11434).WithExternalHttpEndpoints();

// var ollama = builder.AddContainer("ollama", "ai/mxbai-embed-large")
// .WithHttpEndpoint(port: 11434, targetPort: 11434).WithExternalHttpEndpoints();

var agent = builder.AddProject<RaspberryAzure_AgentAnalyzer>("agent")
    .WithExternalHttpEndpoints();

aggregator.WithReference(agent);

var anomalyDetector =
    builder.AddProject<RaspberryAzure_AnomalyDetector>("anomalyDetector");

var reactApp = builder.AddNpmApp("reactvite", "../RaspberryAzure.ReactClient")
    .WithReference(agent)
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();


builder.Build().Run();
