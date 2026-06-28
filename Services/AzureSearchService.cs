using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AzureAISearchTutorialCompanion.Models;

namespace AzureAISearchTutorialCompanion.Services;

internal class AzureSearchService
{
    private const string ApiVersion  = "2024-07-01";
    private const int    BatchSize   = 500;
    private const int    MaxPageSize = 1000;

    // ── Index documents ───────────────────────────────────────────────────────

    public int IndexDocuments(AzureSearchConfig config, List<DocumentChunk> chunks)
    {
        int indexed = 0;

        foreach (var batch in Batch(chunks, BatchSize))
        {
            var actions = batch.Select(c => new Dictionary<string, object?>
            {
                ["@search.action"] = "mergeOrUpload",
                ["id"]             = c.Id,
                ["tenant_id"]      = c.TenantId,
                ["document_id"]    = c.DocumentId,
                ["sourceName"]     = c.SourceName,
                ["section"]        = c.Section,
                ["content"]        = c.Content,
                ["embedding"]      = c.Embedding,
            });

            var body = JsonSerializer.Serialize(new { value = actions });
            PostToIndex(config, "docs/index", body);
            indexed += batch.Count;
        }

        return indexed;
    }

    // ── Delete all chunks for a document_id ───────────────────────────────────

    public int DeleteByDocumentId(AzureSearchConfig config, long documentId)
    {
        var ids     = FetchAllIdsByDocumentId(config, documentId);
        int deleted = 0;

        foreach (var batch in Batch(ids, BatchSize))
        {
            var actions = batch.Select(id => new Dictionary<string, object?>
            {
                ["@search.action"] = "delete",
                ["id"]             = id,
            });

            var body = JsonSerializer.Serialize(new { value = actions });
            PostToIndex(config, "docs/index", body);
            deleted += batch.Count;
        }

        return deleted;
    }

    // ── Search ────────────────────────────────────────────────────────────────

    public List<SearchResultChunk> Search(AzureSearchConfig config, string query, string filter,
        int topN, double minimumScore, float[]? queryVector = null)
    {
        var top = topN > 0 ? topN : 3;

        // Try hybrid search with semantic ranking first; fall back to full-text if the
        // index tier or configuration does not support it (Azure returns 400).
        try
        {
            return ExecuteSearch(config, BuildSemanticRequest(query, filter, top, queryVector), minimumScore);
        }
        catch
        {
            return ExecuteSearch(config, BuildFullTextRequest(query, filter, top, queryVector), minimumScore);
        }
    }

    private static Dictionary<string, object?> BuildSemanticRequest(string query, string filter, int top, float[]? queryVector)
    {
        var body = new Dictionary<string, object?>
        {
            ["search"]                = string.IsNullOrWhiteSpace(query) ? "*" : query,
            ["searchFields"]          = "content",
            ["queryType"]             = "semantic",
            ["semanticConfiguration"] = "default",
            ["top"]                   = top,
            ["select"]                = "tenant_id,document_id,sourceName,section,content",
        };
        if (!string.IsNullOrWhiteSpace(filter)) body["filter"] = filter;
        if (queryVector != null && queryVector.Length > 0)
            body["vectorQueries"] = new[] { new { kind = "vector", vector = queryVector, fields = "embedding", k = top } };
        return body;
    }

    private static Dictionary<string, object?> BuildFullTextRequest(string query, string filter, int top, float[]? queryVector)
    {
        var body = new Dictionary<string, object?>
        {
            ["search"]       = string.IsNullOrWhiteSpace(query) ? "*" : query,
            ["searchFields"] = "content",
            ["queryType"]    = "simple",
            ["searchMode"]   = "any",
            ["top"]          = top,
            ["select"]       = "tenant_id,document_id,sourceName,section,content",
        };
        if (!string.IsNullOrWhiteSpace(filter)) body["filter"] = filter;
        if (queryVector != null && queryVector.Length > 0)
            body["vectorQueries"] = new[] { new { kind = "vector", vector = queryVector, fields = "embedding", k = top } };
        return body;
    }

    private List<SearchResultChunk> ExecuteSearch(AzureSearchConfig config, Dictionary<string, object?> request, double minimumScore)
    {
        var body     = JsonSerializer.Serialize(request);
        var response = PostToSearch(config, "docs/search", body);
        var parsed   = JsonSerializer.Deserialize<AzureSearchResponse>(response);

        return parsed?.Value?
            .Select(d => new SearchResultChunk
            {
                TenantId   = d.TenantId,
                DocumentId = d.DocumentId,
                SourceName = d.SourceName ?? string.Empty,
                Section    = d.Section    ?? string.Empty,
                Content    = d.Content    ?? string.Empty,
                Score      = d.RerankerScore ?? d.Score,  // prefer semantic reranker score; fall back to base score
            })
            .Where(r => r.Score >= minimumScore)
            .ToList() ?? new List<SearchResultChunk>();
    }

    // ── Fetch all IDs for a document_id (handles pagination) ─────────────────

    private List<string> FetchAllIdsByDocumentId(AzureSearchConfig config, long documentId)
    {
        var ids  = new List<string>();
        int skip = 0;

        while (true)
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                search = "*",
                filter = $"document_id eq {documentId}L",
                select = "id",
                top    = MaxPageSize,
                skip,
            });

            var response = PostToSearch(config, "docs/search", requestBody);
            var parsed   = JsonSerializer.Deserialize<AzureSearchResponse>(response);
            var page     = parsed?.Value;

            if (page == null || page.Count == 0)
                break;

            ids.AddRange(page.Select(d => d.Id ?? string.Empty).Where(id => id.Length > 0));

            if (page.Count < MaxPageSize)
                break;

            skip += MaxPageSize;
        }

        return ids;
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────

    private string PostToSearch(AzureSearchConfig config, string path, string jsonBody)
        => SendRequest(config, path, jsonBody, useAdminKey: false);

    private void PostToIndex(AzureSearchConfig config, string path, string jsonBody)
        => SendRequest(config, path, jsonBody, useAdminKey: true);

    private string SendRequest(AzureSearchConfig config, string path, string jsonBody, bool useAdminKey)
    {
        var url = $"{config.Endpoint.TrimEnd('/')}/indexes/{config.IndexName}/{path}?api-version={ApiVersion}";

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var apiKey       = useAdminKey ? config.PrimaryAdminKey : config.QueryKey;
        client.DefaultRequestHeaders.Add("api-key", apiKey);

        var content  = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        content.Headers.ContentType!.CharSet = null;
        var response = client.PostAsync(url, content).GetAwaiter().GetResult();
        var body     = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Azure AI Search error (HTTP {(int)response.StatusCode}): {body}");
        }

        return body;
    }

    private static IEnumerable<List<T>> Batch<T>(IEnumerable<T> source, int size)
    {
        var batch = new List<T>(size);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count == size)
            {
                yield return batch;
                batch = new List<T>(size);
            }
        }
        if (batch.Count > 0)
            yield return batch;
    }
}
