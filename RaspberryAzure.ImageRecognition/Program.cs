using Microsoft.EntityFrameworkCore;
using RaspberryAzure.ImageRecognition;
using RaspberryAzure.ImageRecognition.Endpoints;
using RaspberryAzure.ImageRecognition.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<EmbeddingGenerator>();

var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
var visionModel   = builder.Configuration["Ollama:VisionModel"] ?? "llava";
builder.Services.AddWorkflow("workflowAgent", ollamaBaseUrl, visionModel);

builder.AddSqlServerDbContext<SceneSnapshotDbContext>("SceneDb",
    configureDbContextOptions: options =>
        options.UseSqlServer(o => o.UseCompatibilityLevel(170)));

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();

app.MapImageRecognitionEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SceneSnapshotDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
