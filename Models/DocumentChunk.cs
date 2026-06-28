using System.Text.Json.Serialization;

namespace AzureAISearchTutorialCompanion.Models;

internal class DocumentChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("tenant_id")]
    public long TenantId { get; set; }

    [JsonPropertyName("document_id")]
    public long DocumentId { get; set; }

    [JsonPropertyName("sourceName")]
    public string SourceName { get; set; } = string.Empty;

    [JsonPropertyName("section")]
    public string Section { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
