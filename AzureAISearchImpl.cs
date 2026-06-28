using System.Text.Json;
using AzureAISearchTutorialCompanion.Models;
using AzureAISearchTutorialCompanion.Services;

namespace AzureAISearchTutorialCompanion;

public class AzureAISearchImpl : IAzureAISearch
{
    private readonly PdfChunkerService   _chunkerService   = new();
    private readonly AzureSearchService  _searchService    = new();
    private readonly EmbeddingService    _embeddingService = new();

    // ── Index_Document ────────────────────────────────────────────────────────

    public void Index_Document(
        byte[]     PDFBinary,
        string     AzureEndpoint,
        string     AzureAdminKey,
        string     AzureIndexName,
        bool       PerformEmbedding,
        string     AzureOpenAIEndpoint,
        string     AzureOpenAIKey,
        string     EmbeddingDeploymentName,
        long       TenantId,
        long       DocumentId,
        string     SourceName,
        out bool   IsSuccess,
        out string Message,
        out int    TotalChunksIndexed)
    {
        IsSuccess          = false;
        Message            = string.Empty;
        TotalChunksIndexed = 0;

        try
        {
            ValidateAzureInputs(AzureEndpoint, AzureAdminKey, AzureIndexName);
            if (PerformEmbedding)
                ValidateOpenAIInputs(AzureOpenAIEndpoint, AzureOpenAIKey, EmbeddingDeploymentName);
            if (PDFBinary == null || PDFBinary.Length == 0)
                throw new ArgumentException("PDFBinary cannot be empty.");

            var config = new AzureSearchConfig
            {
                Endpoint        = AzureEndpoint,
                PrimaryAdminKey = AzureAdminKey,
                QueryKey        = AzureAdminKey,
                IndexName       = AzureIndexName,
            };

            var rawChunks = _chunkerService.ChunkPdf(PDFBinary);
            if (rawChunks.Count == 0)
                throw new InvalidOperationException("No text could be extracted from the PDF. Ensure the PDF contains selectable text (not scanned images).");

            _searchService.DeleteByDocumentId(config, DocumentId);

            var documents = rawChunks.Select(c => new DocumentChunk
            {
                Id         = Guid.NewGuid().ToString(),
                TenantId   = TenantId,
                DocumentId = DocumentId,
                SourceName = SourceName,
                Section    = c.Section,
                Content    = c.Content,
                Embedding  = PerformEmbedding
                    ? _embeddingService.GetEmbedding(c.Content, AzureOpenAIEndpoint, AzureOpenAIKey, EmbeddingDeploymentName)
                    : Array.Empty<float>(),
            }).ToList();

            TotalChunksIndexed = _searchService.IndexDocuments(config, documents);

            IsSuccess = true;
            Message   = $"Successfully indexed {TotalChunksIndexed} chunks for source '{SourceName}'" +
                        (PerformEmbedding ? " with embeddings." : " (text only, no embeddings).");
        }
        catch (ArgumentException ex)
        {
            Message = $"Invalid input: {ex.Message}";
        }
        catch (Exception ex)
        {
            Message = $"Unexpected error during indexing: {ex.Message}";
        }
    }

    // ── Delete_Document ───────────────────────────────────────────────────────

    public void Delete_Document(
        string     AzureEndpoint,
        string     AzureAdminKey,
        string     AzureIndexName,
        long       DocumentId,
        out bool   IsSuccess,
        out string Message,
        out int    TotalChunksDeleted)
    {
        IsSuccess          = false;
        Message            = string.Empty;
        TotalChunksDeleted = 0;

        try
        {
            ValidateAzureInputs(AzureEndpoint, AzureAdminKey, AzureIndexName);

            var config = new AzureSearchConfig
            {
                Endpoint        = AzureEndpoint,
                PrimaryAdminKey = AzureAdminKey,
                QueryKey        = AzureAdminKey,
                IndexName       = AzureIndexName,
            };

            TotalChunksDeleted = _searchService.DeleteByDocumentId(config, DocumentId);

            IsSuccess = true;
            Message   = $"Successfully deleted {TotalChunksDeleted} chunks for Document_Id '{DocumentId}'.";
        }
        catch (ArgumentException ex)
        {
            Message = $"Invalid input: {ex.Message}";
        }
        catch (Exception ex)
        {
            Message = $"Unexpected error during deletion: {ex.Message}";
        }
    }

    // ── Search_Document ───────────────────────────────────────────────────────

    public void Search_Document(
        string     AzureEndpoint,
        string     AzureAdminKey,
        string     AzureIndexName,
        bool       PerformEmbedding,
        string     AzureOpenAIEndpoint,
        string     AzureOpenAIKey,
        string     EmbeddingDeploymentName,
        long       TenantId,
        string     SearchQuery,
        int        ReturnTopNChunks,
        double     MinimumRelevanceScore,
        out bool   IsSuccess,
        out string Message,
        out string JSON)
    {
        IsSuccess = false;
        Message   = string.Empty;
        JSON      = "[]";

        try
        {
            ValidateAzureInputs(AzureEndpoint, AzureAdminKey, AzureIndexName);
            if (PerformEmbedding)
                ValidateOpenAIInputs(AzureOpenAIEndpoint, AzureOpenAIKey, EmbeddingDeploymentName);
            if (string.IsNullOrWhiteSpace(SearchQuery))
                throw new ArgumentException("SearchQuery cannot be empty.");
            if (ReturnTopNChunks <= 0)
                throw new ArgumentException("ReturnTopNChunks must be greater than 0.");
            if (MinimumRelevanceScore < 0 || MinimumRelevanceScore > 1)
                throw new ArgumentException("MinimumRelevanceScore must be between 0.0 and 1.0.");

            var config = new AzureSearchConfig
            {
                Endpoint        = AzureEndpoint,
                PrimaryAdminKey = AzureAdminKey,
                QueryKey        = AzureAdminKey,
                IndexName       = AzureIndexName,
            };

            float[]? queryVector = PerformEmbedding
                ? _embeddingService.GetEmbedding(SearchQuery, AzureOpenAIEndpoint, AzureOpenAIKey, EmbeddingDeploymentName)
                : null;

            var filter  = $"tenant_id eq {TenantId}L";
            var results = _searchService.Search(config, SearchQuery, filter, ReturnTopNChunks, MinimumRelevanceScore, queryVector);

            JSON = JsonSerializer.Serialize(results.Select(r => new
            {
                content        = r.Content,
                relevanceScore = Math.Round(r.Score, 4),
            }).ToList());

            IsSuccess = true;
            Message   = $"Found {results.Count} chunk(s) with relevance score >= {MinimumRelevanceScore}.";
        }
        catch (ArgumentException ex)
        {
            Message = $"Invalid input: {ex.Message}";
        }
        catch (Exception ex)
        {
            Message = $"Unexpected error during search: {ex.Message}";
        }
    }

    // ── Shared validation ─────────────────────────────────────────────────────

    private static void ValidateAzureInputs(string endpoint, string adminKey, string indexName)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("AzureEndpoint cannot be empty.");
        if (string.IsNullOrWhiteSpace(adminKey))
            throw new ArgumentException("AzureAdminKey cannot be empty.");
        if (string.IsNullOrWhiteSpace(indexName))
            throw new ArgumentException("AzureIndexName cannot be empty.");
    }

    private static void ValidateOpenAIInputs(string endpoint, string apiKey, string deploymentName)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("AzureOpenAIEndpoint cannot be empty.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("AzureOpenAIKey cannot be empty.");
        if (string.IsNullOrWhiteSpace(deploymentName))
            throw new ArgumentException("EmbeddingDeploymentName cannot be empty.");
    }
}
