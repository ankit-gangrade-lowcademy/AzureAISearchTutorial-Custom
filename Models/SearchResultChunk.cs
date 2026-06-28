using System.Text.Json.Serialization;

namespace AzureAISearchTutorialCompanion.Models;

internal class SearchResultChunk
{
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

    public double Score { get; set; }
}

internal class AzureSearchResponse
{
    [JsonPropertyName("value")]
    public List<AzureSearchDocument>? Value { get; set; }
}

internal class AzureSearchDocument
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public long TenantId { get; set; }

    [JsonPropertyName("document_id")]
    public long DocumentId { get; set; }

    [JsonPropertyName("sourceName")]
    public string? SourceName { get; set; }

    [JsonPropertyName("section")]
    public string? Section { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("@search.score")]
    public double Score { get; set; }

    [JsonPropertyName("@search.rerankerScore")]
    public double? RerankerScore { get; set; }
}
