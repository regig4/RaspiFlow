using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using SkiaSharp;

var builder = WebApplication.CreateBuilder(args);

static AIAgent CreateAgent(string name, string instructions) =>
    new ChatClientAgent(
        new OllamaApiClient(new Uri("http://localhost:11434"), "llava"),
        instructions: instructions,
        name: name);

builder.Services.AddOpenApi();

builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton(_ => new OllamaApiClient(new Uri("http://localhost:11434")));

builder.Services.AddKeyedSingleton<AIAgent>("workflowAgent", (_, _) =>
{
    var descriptionAgent = CreateAgent(
        name: "DescriptionAgent",
        instructions: """
                      You are a precise image analyst.
                      Write one clear paragraph (3-5 sentences) describing what you see:
                      objects, colours, spatial relationships, mood, and notable context.
                      Output ONLY the description paragraph.
                      """);
 
    var tagsAgent = CreateAgent(
        name: "TagsAgent",
        instructions: """
                      You are a content-tagging specialist.
                      You will receive a description of an image written by a previous agent.
                      Output a comma-separated list of lowercase ONE WORD tags (5-10 tags).
                      Example: cat, indoor, sleeping, grey, cosy
                      Output ONLY the comma-separated list, no extra text.
                      """);
 
    var labelAgent = CreateAgent(
        name: "LabelAgent",
        instructions: """
                      You are a classification specialist.
                      You will receive a description and tags for an image from previous agents.
                      Output exactly ONE short general label (e.g. animal, food, landscape,
                      architecture, person, vehicle, document, sport, art, technology).
                      Output ONLY the single label word, lowercase, no punctuation.
                      """);
 
    return AgentWorkflowBuilder
        .BuildSequential(descriptionAgent, tagsAgent, labelAgent)
        .AsAIAgent();
});

builder.AddSqlServerDbContext<SceneSnapshotDbContext>("SceneDb",
    configureDbContextOptions: options =>
        options.UseSqlServer(o => o.UseCompatibilityLevel(170)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/label-image", async (
        HttpRequest request,
        [FromKeyedServices("workflowAgent")] AIAgent workflowAgent,
        EmbeddingService embeddingService,
        SceneSnapshotDbContext dbContext) =>
    {
        if (!request.HasFormContentType)
            return Results.BadRequest("Expected multipart/form-data with an 'image' file.");
 
        var form = await request.ReadFormAsync();
        var file = form.Files["image"];
 
        if (file is null || file.Length == 0)
            return Results.BadRequest("No image file found in the 'image' field.");
 
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var imageBytes = ms.ToArray();
        
        // Pixel diff (prefilter in order to avoid running LLM every time)
        var lastImagePath = "last.jpg";
        if (File.Exists(lastImagePath))
        {
            var lastBytes = await File.ReadAllBytesAsync(lastImagePath);
            var diff = ComputePixelDiff(lastBytes, imageBytes);
            if (diff < 0.05f) // less than 5% of pixels changed
                return Results.Ok("No change detected (pixel diff)");
        }
 
        var mediaType = file.ContentType switch
        {
            "image/png"  => "image/png",
            "image/webp" => "image/webp",
            "image/gif"  => "image/gif",
            _            => "image/jpeg"
        };
 
        var userMessage = new ChatMessage(ChatRole.User,
        [
            new DataContent(imageBytes, mediaType),
            new TextContent("Analyse this image carefully.")
        ]);
 
        var session  = await workflowAgent.CreateSessionAsync();
        var response = await workflowAgent.RunAsync(userMessage, session);
 
        string description = string.Empty;
        string tagsRaw     = string.Empty;
        string label       = string.Empty;
 
        foreach (var msg in response.Messages)
        {
            switch (msg.AuthorName)
            {
                case "DescriptionAgent": description = msg.Text?.Trim() ?? string.Empty; break;
                case "TagsAgent":        tagsRaw     = msg.Text?.Trim() ?? string.Empty; break;
                case "LabelAgent":       label       = msg.Text?.Trim().ToLowerInvariant() ?? string.Empty; break;
            }
        }
 
        var tags = tagsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToList();

        // Embedding similarity (semantic check)
        var descriptionEmbedding = await embeddingService.GetEmbeddingAsync(description);

        var lastSnapshot = await dbContext.Snapshots
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastSnapshot?.DescriptionEmbedding != null)
        {
            var similarity = CosineSimilarity(
                lastSnapshot.DescriptionEmbedding.Value.Memory.Span,
                descriptionEmbedding.Memory.Span);

            if (similarity > 0.95f)
                return Results.Ok("No significant change detected (semantic similarity)");
        }
        
        var sceneSnapshot = new SceneSnapshot 
        {
            Label = label, 
            Description = description, 
            Tags = tags, 
            CreatedAt = DateTime.Now,
            DescriptionEmbedding = descriptionEmbedding
        };

        dbContext.Snapshots.Add(sceneSnapshot);
        await dbContext.SaveChangesAsync();
        
        return Results.Ok(sceneSnapshot);
    })
    .WithName("LabelImage")
    .Accepts<IFormFile>("multipart/form-data")
    .Produces<SceneSnapshot>();


app.MapPost("/query", async (
    QueryRequest request,
    SceneSnapshotDbContext db,
    EmbeddingService embeddingService,
    OllamaApiClient ollama) =>
{
    var queryVector = await embeddingService.GetEmbeddingAsync(request.Question);

    var relevant = await db.Snapshots
        .OrderBy(s => EF.Functions.VectorDistance("cosine", (SqlVector<float>)s.DescriptionEmbedding, queryVector))
        .Take(5)
        .Select(s => new { s.Description, s.Tags, s.Label, s.CreatedAt })
        .ToListAsync();

    if (!relevant.Any())
        return Results.Ok("Brak danych w bazie.");

    var context = string.Join("\n\n", relevant.Select((s, i) =>
        "[" + (i + 1) + "] " + s.CreatedAt.ToString("yyyy-MM-dd HH:mm") + " | " + s.Label + " | " 
        + s.Description + " | Tags: " + string.Join(", ", s.Tags as List<string>)).ToList() as List<string>);

    var prompt = $"""
                  You are an assistant analyzing security camera recordings.
                  Answer the user's question based on the camera records provided below.
                  If there is insufficient data to answer, say so clearly.

                  Camera records:
                  {context}

                  Question: {request.Question}
                  """;

    ollama.SelectedModel = "llava";
    var response = new StringBuilder();
    
    await foreach (var chunk in ollama.GenerateAsync(new GenerateRequest { Prompt = prompt }))
        response.Append(chunk.Response);

    return Results.Ok(new 
    { 
        Answer = response.ToString(),
        Sources = relevant.Select(s => new { s.CreatedAt, s.Label })
    });
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SceneSnapshotDbContext>();
    await db.Database.MigrateAsync();
}

float ComputePixelDiff(byte[] lastBytes, byte[] incomingBytes)
{
    using var lastBitmap     = SKBitmap.Decode(lastBytes);
    using var incomingBitmap = SKBitmap.Decode(incomingBytes);

    // Resize do 64x64 i skala szarości
    var info = new SKImageInfo(64, 64, SKColorType.Gray8);
    using var lastResized     = lastBitmap.Resize(info, SKFilterQuality.Low);
    using var incomingResized = incomingBitmap.Resize(info, SKFilterQuality.Low);

    int totalPixels  = 64 * 64;
    int changedPixels = 0;

    var lastSpan     = lastResized.GetPixelSpan();
    var incomingSpan = incomingResized.GetPixelSpan();

    for (int i = 0; i < lastSpan.Length; i++)
    {
        var diff = Math.Abs(lastSpan[i] - incomingSpan[i]);
        if (diff > 30)
            changedPixels++;
    }

    return (float)changedPixels / totalPixels;
}

float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
{
    float dot = 0, magA = 0, magB = 0;
    for (int i = 0; i < a.Length; i++)
    {
        dot  += a[i] * b[i];
        magA += a[i] * a[i];
        magB += b[i] * b[i];
    }
    return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
}

app.Run();

record QueryRequest(string Question);
