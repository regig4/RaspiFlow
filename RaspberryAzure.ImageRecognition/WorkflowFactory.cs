using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using OllamaSharp;

namespace RaspberryAzure.ImageRecognition;

public static class WorkflowFactory
{
    public static void AddWorkflow(this IServiceCollection services, string workflowName, string ollamaBaseUrl, string visionModel)
    {
        services.AddKeyedSingleton<AIAgent>(workflowName, (_, _) =>
        {
            var descriptionAgent = CreateAgent(
                name: "DescriptionAgent",
                instructions: """
                              You are a precise image analyst.
                              Write one clear paragraph (3-5 sentences) describing what you see:
                              objects, colours, spatial relationships, mood, and notable context.
                              Output ONLY the description paragraph.
                              """,
                ollamaBaseUrl: ollamaBaseUrl,
                visionModel: visionModel);

            var tagsAgent = CreateAgent(
                name: "TagsAgent",
                instructions: """
                              You are a content-tagging specialist.
                              You will receive a description of an image written by a previous agent.
                              Output a comma-separated list of lowercase ONE WORD tags (5-10 tags).
                              Example: cat, indoor, sleeping, grey, cosy
                              Output ONLY the comma-separated list, no extra text.
                              """,
                ollamaBaseUrl: ollamaBaseUrl,
                visionModel: visionModel);

            var labelAgent = CreateAgent(
                name: "LabelAgent",
                instructions: """
                              You are a classification specialist.
                              You will receive a description and tags for an image from previous agents.
                              Output exactly ONE short general label (e.g. animal, food, landscape,
                              architecture, person, vehicle, document, sport, art, technology).
                              Output ONLY the single label word, lowercase, no punctuation.
                              """,
                ollamaBaseUrl: ollamaBaseUrl,
                visionModel: visionModel);

            return AgentWorkflowBuilder
                .BuildSequential(descriptionAgent, tagsAgent, labelAgent)
                .AsAIAgent();
        });
    }

    private static AIAgent CreateAgent(string name, string instructions, string ollamaBaseUrl, string visionModel) =>
        new ChatClientAgent(
            new OllamaApiClient(new Uri(ollamaBaseUrl), visionModel),
            instructions: instructions,
            name: name);
}
