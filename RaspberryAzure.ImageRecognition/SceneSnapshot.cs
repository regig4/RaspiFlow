using Microsoft.Data.SqlTypes;

namespace RaspberryAzure.ImageRecognition;

public class SceneSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public SqlVector<float>? DescriptionEmbedding { get; set; }
}
