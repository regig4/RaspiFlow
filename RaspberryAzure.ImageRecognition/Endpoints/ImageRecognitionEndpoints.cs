using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using RaspberryAzure.ImageRecognition.Services;

namespace RaspberryAzure.ImageRecognition.Endpoints;

public static class ImageRecognitionEndpoints
{
    public static void MapImageRecognitionEndpoints(this WebApplication app)
    {
        app.MapPost("/label-image", LabelImageHandler)
            .WithName("LabelImage")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<SceneSnapshot>();

        app.MapPost("/query", QueryHandler)
            .WithName("Query");
    }

    private static async Task<IResult> LabelImageHandler(
        HttpRequest request,
        [FromKeyedServices("workflowAgent")] AIAgent workflowAgent,
        EmbeddingGenerator embeddingGenerator,
        SceneSnapshotDbContext dbContext)
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

        var lastImagePath = "last.jpg";
        if (File.Exists(lastImagePath))
        {
            var lastBytes = await File.ReadAllBytesAsync(lastImagePath);
            var diff = ImageComparer.ComputePixelDiff(lastBytes, imageBytes);
            if (diff < 0.05f)
                return Results.Ok("No change detected (pixel diff)");
        }

        await File.WriteAllBytesAsync(lastImagePath, imageBytes);

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

        var descriptionEmbedding = await embeddingGenerator.GetEmbeddingAsync(description);

        var lastSnapshot = await dbContext.Snapshots
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastSnapshot?.DescriptionEmbedding != null)
        {
            var similarity = DescriptionComparer.CosineSimilarity(
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
            CreatedAt = DateTime.UtcNow,
            DescriptionEmbedding = descriptionEmbedding
        };

        dbContext.Snapshots.Add(sceneSnapshot);
        await dbContext.SaveChangesAsync();

        return Results.Ok(sceneSnapshot);
    }

    private static async Task<IResult> QueryHandler(
        QueryRequest request,
        SceneSnapshotDbContext db,
        EmbeddingGenerator embeddingGenerator,
        IConfiguration configuration)
    {
        var queryVector = await embeddingGenerator.GetEmbeddingAsync(request.Question);

        var relevant = await db.Snapshots
            .Where(s => s.DescriptionEmbedding != null)
            .OrderBy(s => EF.Functions.VectorDistance("cosine", (SqlVector<float>)s.DescriptionEmbedding!, queryVector))
            .Take(5)
            .Select(s => new { s.Description, s.Tags, s.Label, s.CreatedAt })
            .ToListAsync();

        if (!relevant.Any())
            return Results.Ok("No data in the database.");

        var context = string.Join("\n\n", relevant.Select((s, i) =>
            $"[{i + 1}] {s.CreatedAt:yyyy-MM-dd HH:mm} | {s.Label} | {s.Description} | Tags: {string.Join(", ", s.Tags ?? [])}"));

        var prompt = $"""
                      You are an assistant analyzing security camera recordings.
                      Answer the user's question based on the camera records provided below.
                      If there is insufficient data to answer, say so clearly.

                      Camera records:
                      {context}

                      Question: {request.Question}
                      """;

        var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var textModel = configuration["Ollama:TextModel"] ?? "llama3.2";
        var ollama = new OllamaApiClient(new Uri(ollamaBaseUrl)) { SelectedModel = textModel };

        var answer = new StringBuilder();
        await foreach (var chunk in ollama.GenerateAsync(new GenerateRequest { Prompt = prompt }))
            answer.Append(chunk?.Response ?? string.Empty);

        return Results.Ok(new
        {
            Answer = answer.ToString(),
            Sources = relevant.Select(s => new { s.CreatedAt, s.Label })
        });
    }
}

record QueryRequest(string Question);
