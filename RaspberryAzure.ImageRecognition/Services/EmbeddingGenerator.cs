using Microsoft.Data.SqlTypes;
using OllamaSharp;
using OllamaSharp.Models;

public class EmbeddingGenerator
{
    private readonly OllamaApiClient _ollama;

    public EmbeddingGenerator()
    {
        _ollama = new OllamaApiClient(new Uri("http://localhost:11434"));
        _ollama.SelectedModel = "nomic-embed-text";
    }

    public async Task<SqlVector<float>> GetEmbeddingAsync(string text)
    {
        var response = await _ollama.EmbedAsync(new EmbedRequest
        {
            Model = "nomic-embed-text",
            Input = [text]
        });

        var floats = response.Embeddings[0].Select(d => (float)d).ToArray();
        return new SqlVector<float>(floats);
    }
}