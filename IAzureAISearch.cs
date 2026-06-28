using OutSystems.ExternalLibraries.SDK;

namespace AzureAISearchTutorialCompanion;

[OSInterface(
    Name = "AzureAISearch_Custom",
    IconResourceName = "AzureAISearchTutorialCompanion.icon.png",
    Description = "Azure AI Search Tutorial Custom is a learning-focused project for OutSystems developers to understand Azure AI Search and Retrieval-Augmented Generation (RAG). " +
                  "It is an enhanced version of the AzureAISearchTutorial project that guides you through setting up your own Azure environment, including Azure AI Search, Azure OpenAI, model deployments, embeddings, indexes, and related resources. " +
                  "Connect your own Azure AI Search service to OutSystems ODC. " +
                  "Pass your Azure credentials directly — no shared sandbox, no third-party broker. " +
                  "Supports chunking PDF documents, generating embeddings via Azure OpenAI, indexing content with " +
                  "tenant and document isolation, performing hybrid vector + semantic search, and building " +
                  "Retrieval-Augmented Generation (RAG) workflows.\n\n" +
                  "Prerequisites:\n" +
                  "1. An active Azure subscription with an Azure AI Search service created.\n" +
                  "2. A search index with the following fields: id (Edm.String, key), tenant_id (Edm.Int64, filterable), " +
                  "document_id (Edm.Int64, filterable), sourceName (Edm.String), section (Edm.String), " +
                  "content (Edm.String, searchable), " +
                  "embedding (Collection(Edm.Single), searchable, dimensions must match your embedding model).\n" +
                  "3. A vectorSearch configuration (e.g. HNSW algorithm) and semantic configuration named 'default' on the index.\n" +
                  "4. An Azure OpenAI resource with an embedding model deployment (e.g. text-embedding-3-small).\n" +
                  "5. Your Search service Endpoint URL, Admin Key, and Index Name — all available in the Azure Portal.\n\n" +
                  "── About the Author ──────────────────────────────────────────────\n" +
                  "Ankit Gangrade is an Enterprise OutSystems Architect, AI & Low-Code Architect. " +
                  "He is also the creator of Lowcademy, a learning platform focused on helping developers master Low-Code development " +
                  "through practical tutorials, real-world projects, and hands-on learning.\n\n" +
                  "Connect with Ankit:\n" +
                  "  https://ankitg.in\n" +
                  "  https://linkedin.com/in/ankitgangrade\n" +
                  "  https://youtube.com/@lowcademy\n" +
                  "  https://lowcademy.com"
)]
public interface IAzureAISearch
{
    [OSAction(
        Description = "Chunks a PDF binary into logical sections (700–1200 characters each) and indexes them into Azure AI Search. " +
                      "Before indexing, all existing chunks for the given Document_Id are deleted to ensure fresh data. " +
                      "When PerformEmbedding is True, an embedding vector is generated for each chunk via Azure OpenAI and stored in the index, " +
                      "enabling vector search later. Chunks indexed with PerformEmbedding=False cannot be vector-searched; " +
                      "use the same PerformEmbedding value consistently between indexing and searching. " +
                      "When PerformEmbedding is False, chunks are indexed as plain text only — no OpenAI call is made. " +
                      "Requires an Admin Key with write access to the index."
    )]
    void Index_Document(
        [OSParameter(DataType = OSDataType.BinaryData,
            Description = "The PDF file as BinaryData. Provide the binary content of the PDF stored in your OutSystems database. " +
                          "The PDF must contain selectable text — scanned image-only PDFs will produce no output.")]
        byte[] PDFBinary,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The HTTPS URL of your Azure AI Search service. " +
                          "To find it: Azure Portal → your Search service → Overview tab → copy the URL field. " +
                          "Format: https://<service-name>.search.windows.net — do not include a trailing slash.")]
        string AzureEndpoint,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The Primary or Secondary Admin Key for your Azure AI Search service. " +
                          "To find it: Azure Portal → your Search service → Settings → Keys → Primary admin key. " +
                          "This key grants full read/write access to the index — treat it as a secret and never expose it on the client side.")]
        string AzureAdminKey,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The exact name of the search index to write to. " +
                          "To find it: Azure Portal → your Search service → Search management → Indexes. " +
                          "The index must already exist with fields: id, tenant_id, document_id, sourceName, section, content, embedding (Collection(Edm.Single)). " +
                          "Both tenant_id and document_id must be marked as Filterable.")]
        string AzureIndexName,

        [OSParameter(DataType = OSDataType.Boolean,
            Description = "Controls whether embedding vectors are generated and stored for each chunk. " +
                          "Set to True to call Azure OpenAI and store a float vector in the embedding field — required if you intend to use vector search later. " +
                          "Set to False to skip the Azure OpenAI call entirely and index plain text only; the embedding field will be left empty. " +
                          "Important: chunks indexed with False cannot be vector-searched later. " +
                          "When False, the AzureOpenAIEndpoint, AzureOpenAIKey, and EmbeddingDeploymentName parameters are ignored.")]
        bool PerformEmbedding,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The HTTPS URL of your Azure OpenAI resource. Required only when PerformEmbedding is True. " +
                          "To find it: Azure Portal → your OpenAI resource → Keys and Endpoint → Endpoint, " +
                          "OR from Azure AI Foundry home screen under 'Azure OpenAI endpoint'. " +
                          "Supported formats: https://<name>.openai.azure.com  OR  https://<name>.cognitiveservices.azure.com — do not include a trailing slash.")]
        string AzureOpenAIEndpoint,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The API key for your Azure OpenAI resource. Required only when PerformEmbedding is True. " +
                          "To find it: Azure Portal → your OpenAI resource → Keys and Endpoint → KEY 1. " +
                          "If using Azure AI Foundry, use the key from the same resource whose endpoint you entered above — " +
                          "the Foundry project API key and the resource key are different; use the resource key. " +
                          "Keep this secret and never expose it on the client side.")]
        string AzureOpenAIKey,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The deployment name of your embedding model in Azure OpenAI. Required only when PerformEmbedding is True. " +
                          "To find it: Azure OpenAI Studio (oai.azure.com) → Deployments, OR Azure AI Foundry → your project → Deployments. " +
                          "This is the deployment name you gave the model, which may differ from the model name itself. " +
                          "Must be an embedding model such as text-embedding-3-small, text-embedding-3-large, or text-embedding-ada-002. " +
                          "The model's output dimensions must match the dimensions configured on the embedding field in your Azure AI Search index.")]
        string EmbeddingDeploymentName,

        [OSParameter(DataType = OSDataType.LongInteger,
            Description = "A Long Integer that identifies the tenant (organisation or user group) this document belongs to. " +
                          "Used to scope search results — only chunks with a matching Tenant_Id are returned during search. " +
                          "Pass the Id of the relevant Tenant entity from your OutSystems database.")]
        long TenantId,

        [OSParameter(DataType = OSDataType.LongInteger,
            Description = "A Long Integer that uniquely identifies the source document within your system. " +
                          "All existing chunks for this Document_Id are deleted before re-indexing to prevent duplicates. " +
                          "Pass the Id of the document entity from your OutSystems database.")]
        long DocumentId,

        [OSParameter(DataType = OSDataType.Text,
            Description = "A human-readable label for the document source (e.g. 'AzureGuide.pdf', 'Q3-Report'). " +
                          "Stored in the index for traceability — helps identify where a retrieved chunk came from. " +
                          "Not used for filtering or search ranking.")]
        string SourceName,

        [OSParameter(DataType = OSDataType.Boolean,
            Description = "True if the operation completed without errors; False otherwise.")]
        out bool IsSuccess,

        [OSParameter(DataType = OSDataType.Text,
            Description = "Human-readable result. On success, confirms how many chunks were indexed and whether embeddings were generated. " +
                          "On failure, contains the error detail for troubleshooting.")]
        out string Message,

        [OSParameter(DataType = OSDataType.Integer,
            Description = "Number of text chunks successfully written to Azure AI Search.")]
        out int TotalChunksIndexed
    );

    [OSAction(
        Description = "Permanently deletes all document chunks from Azure AI Search that belong to the given Document_Id. " +
                      "All fields stored on each chunk — including any embedding vectors — are removed as part of the deletion. " +
                      "Requires an Admin Key with write access to the index. " +
                      "This operation is permanent and cannot be undone."
    )]
    void Delete_Document(
        [OSParameter(DataType = OSDataType.Text,
            Description = "The HTTPS URL of your Azure AI Search service. " +
                          "To find it: Azure Portal → your Search service → Overview tab → copy the URL field. " +
                          "Format: https://<service-name>.search.windows.net — do not include a trailing slash.")]
        string AzureEndpoint,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The Primary or Secondary Admin Key for your Azure AI Search service. " +
                          "To find it: Azure Portal → your Search service → Settings → Keys → Primary admin key. " +
                          "This key grants full read/write access to the index — treat it as a secret and never expose it on the client side.")]
        string AzureAdminKey,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The exact name of the search index to delete from. " +
                          "To find it: Azure Portal → your Search service → Search management → Indexes.")]
        string AzureIndexName,

        [OSParameter(DataType = OSDataType.LongInteger,
            Description = "A Long Integer that identifies the tenant. Only chunks matching BOTH this Tenant_Id AND the Document_Id are deleted. " +
                          "Ensures cross-tenant isolation — documents belonging to other tenants are never affected. " +
                          "Pass the Id of the relevant Tenant entity from your OutSystems database.")]
        long TenantId,

        [OSParameter(DataType = OSDataType.LongInteger,
            Description = "A Long Integer that uniquely identifies the document whose chunks should be deleted. " +
                          "All index entries matching both this Document_Id and the Tenant_Id are permanently removed, including any stored embedding vectors. " +
                          "Pass the Id of the document entity from your OutSystems database.")]
        long DocumentId,

        [OSParameter(DataType = OSDataType.Boolean,
            Description = "True if the operation completed without errors; False otherwise.")]
        out bool IsSuccess,

        [OSParameter(DataType = OSDataType.Text,
            Description = "Human-readable result. On success, confirms how many chunks were deleted. " +
                          "On failure, contains the error detail for troubleshooting.")]
        out string Message,

        [OSParameter(DataType = OSDataType.Integer,
            Description = "Number of document chunks permanently removed from Azure AI Search.")]
        out int TotalChunksDeleted
    );

    [OSAction(
        Description = "Searches Azure AI Search and returns the top N most relevant document chunks that meet the minimum relevance score. " +
                      "Results are automatically filtered to the given Tenant_Id. " +
                      "When PerformEmbedding is True, the query is converted to a vector via Azure OpenAI and vector search is used — " +
                      "this requires that the indexed chunks also have embedding vectors (i.e. PerformEmbedding was True during indexing). " +
                      "When PerformEmbedding is False, semantic text search is used (no OpenAI call); if semantic ranking is not supported by the index tier, " +
                      "it automatically falls back to full-text search. " +
                      "Use the same PerformEmbedding value that was used when the documents were indexed."
    )]
    void Search_Document(
        [OSParameter(DataType = OSDataType.Text,
            Description = "The HTTPS URL of your Azure AI Search service. " +
                          "To find it: Azure Portal → your Search service → Overview tab → copy the URL field. " +
                          "Format: https://<service-name>.search.windows.net — do not include a trailing slash.")]
        string AzureEndpoint,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The Primary or Secondary Admin Key for your Azure AI Search service. " +
                          "To find it: Azure Portal → your Search service → Settings → Keys → Primary admin key. " +
                          "This key grants full read/write access to the index — treat it as a secret and never expose it on the client side.")]
        string AzureAdminKey,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The exact name of the search index to query. " +
                          "To find it: Azure Portal → your Search service → Search management → Indexes.")]
        string AzureIndexName,

        [OSParameter(DataType = OSDataType.Boolean,
            Description = "Controls whether vector search is used for this query. " +
                          "Set to True to convert the SearchQuery into an embedding vector via Azure OpenAI and perform vector search — " +
                          "only effective if the indexed chunks were also indexed with PerformEmbedding set to True. " +
                          "Set to False to skip the Azure OpenAI call and use semantic text search instead (falling back to full-text search if needed). " +
                          "When False, the AzureOpenAIEndpoint, AzureOpenAIKey, and EmbeddingDeploymentName parameters are ignored.")]
        bool PerformEmbedding,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The HTTPS URL of your Azure OpenAI resource. Required only when PerformEmbedding is True. " +
                          "To find it: Azure Portal → your OpenAI resource → Keys and Endpoint → Endpoint, " +
                          "OR from Azure AI Foundry home screen under 'Azure OpenAI endpoint'. " +
                          "Supported formats: https://<name>.openai.azure.com  OR  https://<name>.cognitiveservices.azure.com — do not include a trailing slash.")]
        string AzureOpenAIEndpoint,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The API key for your Azure OpenAI resource. Required only when PerformEmbedding is True. " +
                          "To find it: Azure Portal → your OpenAI resource → Keys and Endpoint → KEY 1. " +
                          "Must be the key for the same resource whose endpoint you entered above. " +
                          "Keep this secret and never expose it on the client side.")]
        string AzureOpenAIKey,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The deployment name of your embedding model in Azure OpenAI. Required only when PerformEmbedding is True. " +
                          "Must be the same model and deployment that was used during indexing — " +
                          "mixing different embedding models produces incorrect vector comparisons and poor search results. " +
                          "To find it: Azure OpenAI Studio → Deployments, OR Azure AI Foundry → your project → Deployments.")]
        string EmbeddingDeploymentName,

        [OSParameter(DataType = OSDataType.LongInteger,
            Description = "A Long Integer that identifies the tenant whose documents should be searched. " +
                          "Only chunks with a matching tenant_id are considered — results from other tenants are excluded. " +
                          "Pass the Id of the relevant Tenant entity from your OutSystems database.")]
        long TenantId,

        [OSParameter(DataType = OSDataType.Text,
            Description = "The natural-language question or keyword phrase to search for (e.g. 'How do I configure an index?'). " +
                          "Azure AI Search ranks results by relevance to this text. Cannot be empty.")]
        string SearchQuery,

        [OSParameter(DataType = OSDataType.Integer,
            Description = "Maximum number of top-ranked chunks to retrieve from the index before the MinimumRelevanceScore filter is applied. " +
                          "Recommended: 5–10. After score filtering, the number of results returned in JSON may be lower. Must be greater than 0.")]
        int ReturnTopNChunks,

        [OSParameter(DataType = OSDataType.Decimal,
            Description = "Minimum relevance score a chunk must achieve to appear in the results. Chunks that score below this threshold are silently excluded. Default: 1.5. " +
                          "This library always applies Azure AI semantic reranking, so the returned score is the semantic reranker confidence score (0–4) on Basic tier or above — " +
                          "this applies to BOTH text search (PerformEmbedding=False) and vector search (PerformEmbedding=True), since vector candidates are also reranked. " +
                          "Score guide: 1.0 = low relevance, 2.0 = moderate, 3.0 = good, 4.0 = maximum. Recommended starting value: 1.5. " +
                          "On Free tier (semantic search not supported): falls back to base BM25 or RRF-fused score with no fixed upper bound; recommended value: 0.3–0.7. " +
                          "Set to 0.0 to disable filtering and return all top-N results regardless of score.")]
        double MinimumRelevanceScore,

        [OSParameter(DataType = OSDataType.Boolean,
            Description = "True if the search completed without errors; False otherwise. " +
                          "Note: returning zero results (all chunks filtered by MinimumRelevanceScore) is still a success.")]
        out bool IsSuccess,

        [OSParameter(DataType = OSDataType.Text,
            Description = "Human-readable result. On success, reports how many chunks were returned and the minimum score applied. " +
                          "On failure, contains the error detail for troubleshooting.")]
        out string Message,

        [OSParameter(DataType = OSDataType.Text,
            Description = "JSON array of the top-N chunks that passed the MinimumRelevanceScore threshold, ordered by relevance (highest score first). " +
                          "Each element is an object with two fields:\n" +
                          "  content        (Text)    — the raw text of the chunk; pass directly to your AI model as RAG context.\n" +
                          "  relevanceScore (Decimal) — semantic reranker confidence score, rounded to 4 decimal places. Range is 0–4 (Basic tier+); higher is more relevant.\n" +
                          "Returns [] if no chunks meet the minimum score or no results were found.\n" +
                          "Example: [{\"content\":\"Azure AI Search supports...\",\"relevanceScore\":0.8213},{\"content\":\"To create an index...\",\"relevanceScore\":0.7541}]")]
        out string JSON
    );
}
