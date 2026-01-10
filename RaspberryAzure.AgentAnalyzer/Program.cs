using CSnakes.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Server;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using RaspberryAzure.AgentAnalyzer;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var home = Path.Join(Environment.CurrentDirectory, ".");

// for local testing 
// var client = new HttpClient(new LocalAiHttpMessageHandler());
var client = new HttpClient();
client.Timeout = new TimeSpan(10000000000);

var kernelBuilder = Kernel.CreateBuilder();

var pythonAppBuilder = builder.Services
    .WithPython()
    .WithHome(home)
    .WithVirtualEnvironment(Path.Join(home, ".venv"))
    .WithPipInstaller()
    .FromRedistributable();
var env = pythonAppBuilder.Services.BuildServiceProvider().GetRequiredService<IPythonEnvironment>();

kernelBuilder
    // .AddOpenAIChatCompletion("vdelv/phi-2", "apikey", httpClient: client)
    // .AddOpenAIChatCompletion("ai/mxbai-embed-large", "apikey", httpClient: client)
    .AddOpenAIChatCompletion("gpt-4.1", builder.Configuration["OpenApiKey"], httpClient: client)
    .Build();
kernelBuilder.Services.AddSingleton(env);
kernelBuilder.Plugins.AddFromType<BasicStatsPlugin>("basicstats");

var kernel = kernelBuilder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new() 
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var history = new ChatHistory();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// builder.Services.AddOpenTelemetry()
//     .WithTracing(builder =>
//     {
//         builder.AddAspNetCoreInstrumentation();
//         builder.AddConsoleExporter();
//         builder.AddOtlpExporter();
//     });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource(builder.Environment.ApplicationName)
            .AddAspNetCoreInstrumentation()
            // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
            //.AddGrpcClientInstrumentation()
            .AddHttpClientInstrumentation();
    });

    var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    if (useOtlpExporter)
    {
        builder.Services.AddOpenTelemetry().UseOtlpExporter();
    }

    
    builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    // Add all functions from the kernel plugins to the MCP server as tools
    .WithTools(kernel);


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/addData", (List<Record> records) => 
{
    Storage.AddRecords(records);
    return Results.Ok();
})
.WithName("AddData");

app.MapGet("/askAi", async ([FromServices] ILogger<Program> logger , string input) =>
{
    logger.LogInformation("Ask: {input}", input);
    history.AddUserMessage(input);
    var result = await chatCompletionService.GetChatMessageContentAsync(
        history,
        executionSettings: openAIPromptExecutionSettings,
        kernel: kernel);
    history.AddAssistantMessage(result.Content);

    return Results.Ok(result.Content);
}).WithName("AskAi");

app.Run();

public static class Extensions
{
    public static IMcpServerBuilder WithTools(this IMcpServerBuilder builder, Kernel? kernel = null)
    {
        // If plugins are provided directly, add them as tools
        if (kernel is not null)
        {
            foreach (var plugin in kernel.Plugins)
            {
                foreach (var function in plugin)
                {
                    builder.Services.AddSingleton(McpServerTool.Create(function));
                }
            }

            return builder;
        }

        // If no plugins are provided explicitly, add all functions from the kernel plugins registered in DI container as tools
        builder.Services.AddSingleton<IEnumerable<McpServerTool>>(services =>
        {
            IEnumerable<KernelPlugin> plugins = services.GetServices<KernelPlugin>();

            List<McpServerTool> tools = new(plugins.Count());

            foreach (var plugin in plugins)
            {
                foreach (var function in plugin)
                {
                    tools.Add(McpServerTool.Create(function));
                }
            }

            return tools;
        });

        return builder;
    }
}