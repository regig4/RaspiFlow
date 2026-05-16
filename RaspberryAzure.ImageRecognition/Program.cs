using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

static AIAgent CreateAgent(string name, string instructions) =>
    new ChatClientAgent(
        new OllamaApiClient(new Uri("http://localhost:11434"), "llava"),
        instructions: instructions,
        name: name);

builder.Services.AddOpenApi();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/label-image", async (
        HttpRequest request,
        [FromKeyedServices("workflowAgent")] AIAgent workflowAgent) =>
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

        var image = new Image(label, description, tags);
        
        // TODO: Save image in JSON format into PostgreSQL db
        
        return Results.Ok(image);
    })
    .WithName("LabelImage")
    .Accepts<IFormFile>("multipart/form-data")
    .Produces<Image>();
app.Run();

record Image(string Label, string Description, ICollection<string> Tags);

