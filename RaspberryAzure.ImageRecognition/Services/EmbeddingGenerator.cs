using Microsoft.Data.SqlTypes;
using OllamaSharp;
using OllamaSharp.Models;

namespace RaspberryAzure.ImageRecognition.Services;

public class EmbeddingGenerator
{
    private readonly OllamaApiClient _ollama;
    private readonly string _model;

    public EmbeddingGenerator(IConfiguration configuration)
    {
        var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _model = configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
        _ollama = new OllamaApiClient(new Uri(baseUrl));
    }

    public async Task<SqlVector<float>> GetEmbeddingAsync(string text)
    {
        var response = await _ollama.EmbedAsync(new EmbedRequest
        {
            Model = _model,
            Input = [text]
        });

        var floats = response.Embeddings[0].Select(d => (float)d).ToArray();
        return new SqlVector<float>(floats);
    }
}
