# AzureAISearch Custom — OutSystems ODC External Library

A learning-focused **OutSystems ODC External Library** that connects your own Azure AI Search service for full **Retrieval-Augmented Generation (RAG)** workflows. This is an enhanced version of the AzureAISearchTutorial project, guiding OutSystems developers through setting up their own Azure environment and using Azure AI Search APIs to index, search, and manage document chunks.

> **No shared sandbox. No third-party broker. You bring your own Azure credentials.**

---

## Features

- **PDF Chunking** — Automatically splits PDF documents into logical sections (up to 1,000 characters each), respecting paragraph and sentence boundaries and stripping table-of-contents noise
- **Azure OpenAI Embeddings** — Generates vector embeddings per chunk using any Azure OpenAI embedding deployment (`text-embedding-3-small`, `text-embedding-3-large`, `text-embedding-ada-002`)
- **Vector Search** — Performs cosine similarity vector search using HNSW algorithm for semantic accuracy
- **Semantic / Full-Text Fallback** — Automatically falls back from semantic ranking to full-text search when the index tier does not support it
- **Relevance Score Filtering** — Returns a `relevanceScore` per chunk and filters out results below a configurable minimum threshold (default `0.55`)
- **Tenant & Document Isolation** — Every chunk is tagged with `tenant_id` and `document_id` so results are always scoped to the correct tenant and document
- **Clean Re-indexing** — Existing chunks for a document are deleted before re-indexing, preventing duplicates
- **Three OutSystems Actions** — `Index_Document`, `Delete_Document`, `Search_Document`

---

## Prerequisites

### 1. Azure AI Search Service
1. Go to [portal.azure.com](https://portal.azure.com) → Create a resource → **Azure AI Search**
2. Choose a pricing tier (**Basic** or above for semantic search; **Free** tier supports text search only)
3. After creation, go to **Overview** → copy the **URL** (your `AzureEndpoint`)
4. Go to **Settings → Keys** → copy **Primary admin key** (your `AzureAdminKey`)

### 2. Azure AI Search Index
Create an index with the following fields:

| Field | Type | Attributes |
|---|---|---|
| `id` | `Edm.String` | Key, Retrievable |
| `tenant_id` | `Edm.Int64` | Retrievable, **Filterable** |
| `document_id` | `Edm.Int64` | Retrievable, **Filterable** |
| `sourceName` | `Edm.String` | Retrievable |
| `section` | `Edm.String` | Retrievable |
| `content` | `Edm.String` | Retrievable, **Searchable** |
| `embedding` | `Collection(Edm.Single)` | Retrievable, **Searchable**, dimensions = model output (e.g. `1536` for `text-embedding-3-small`) |

> **Important:** `tenant_id` and `document_id` must be marked **Filterable**. Add a **vectorSearch** configuration (HNSW algorithm) and a **semantic configuration** named `default` on the index.

> ⚠️ **This library is built for the exact index structure below. If you use a different field name, type, or attribute, the library will not work correctly and code changes will be required.**

#### Exact Index JSON (use this when creating the index via Azure Portal → JSON editor or REST API)

```json
{
  "name": "ai-search-index",
  "fields": [
    {
      "name": "id",
      "type": "Edm.String",
      "key": true,
      "searchable": false,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false
    },
    {
      "name": "tenant_id",
      "type": "Edm.Int64",
      "searchable": false,
      "filterable": true,
      "retrievable": true,
      "stored": true,
      "sortable": true,
      "facetable": false
    },
    {
      "name": "document_id",
      "type": "Edm.Int64",
      "searchable": false,
      "filterable": true,
      "retrievable": true,
      "stored": true,
      "sortable": true,
      "facetable": false
    },
    {
      "name": "sourceName",
      "type": "Edm.String",
      "searchable": false,
      "filterable": true,
      "retrievable": true,
      "stored": true,
      "sortable": true,
      "facetable": false
    },
    {
      "name": "section",
      "type": "Edm.String",
      "searchable": false,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false
    },
    {
      "name": "content",
      "type": "Edm.String",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false,
      "analyzer": "standard.lucene"
    },
    {
      "name": "embedding",
      "type": "Collection(Edm.Single)",
      "searchable": true,
      "filterable": false,
      "retrievable": false,
      "stored": true,
      "sortable": false,
      "facetable": false,
      "dimensions": 1536,
      "vectorSearchProfile": "hnsw-profile"
    }
  ],
  "vectorSearch": {
    "algorithms": [
      {
        "name": "hnsw-algorithm",
        "kind": "hnsw"
      }
    ],
    "profiles": [
      {
        "name": "hnsw-profile",
        "algorithm": "hnsw-algorithm"
      }
    ]
  },
  "semantic": {
    "configurations": [
      {
        "name": "default",
        "prioritizedFields": {
          "contentFields": [
            { "fieldName": "content" }
          ]
        }
      }
    ]
  }
}
```

> **Note on `dimensions`:** Set `1536` for `text-embedding-3-small` and `text-embedding-ada-002`, or `3072` for `text-embedding-3-large`. This must match the model you deploy in Azure OpenAI.

### 3. Azure OpenAI Resource *(optional — required for vector search)*
1. Go to [portal.azure.com](https://portal.azure.com) → Create a resource → **Azure OpenAI**
2. After creation, go to **Keys and Endpoint** → copy the **Endpoint** and **KEY 1**
3. Open **Azure OpenAI Studio** or **Azure AI Foundry** → **Deployments** → deploy an embedding model (e.g. `text-embedding-3-small`)
4. Note the **deployment name** you gave it — this is your `EmbeddingDeploymentName`

> Supported endpoint formats: `https://<name>.openai.azure.com` and `https://<name>.cognitiveservices.azure.com`

---

## Build & Publish to OutSystems ODC

### Build
```bash
dotnet build
```

### Publish (creates the `.zip` for ODC upload)
```bash
chmod +x publish.sh
./publish.sh
```

This produces `AzureAISearchTutorialCompanion.zip` in the project root.

### Upload to ODC
1. Open **ODC Portal** → **External Logic** → **Manage External Libraries**
2. Click **Upload** and select `AzureAISearchTutorialCompanion.zip`
3. After processing, the three actions will be available in **ODC Studio** under the library name `AzureAISearch_Custom`

### Test Locally
```bash
dotnet run --project TestConsole/TestConsole.csproj
```
The interactive console prompts for all credentials at runtime and runs Index → Search → Delete → Verify in sequence.

---

## Method Reference

### `Index_Document`
Chunks a PDF binary into logical sections and indexes them into Azure AI Search. All existing chunks for the given `DocumentId` are deleted before indexing.

| Parameter | Type | Description |
|---|---|---|
| `PDFBinary` | BinaryData | The PDF file as binary content |
| `AzureEndpoint` | Text | `https://<name>.search.windows.net` |
| `AzureAdminKey` | Text | Admin key from Azure Portal |
| `AzureIndexName` | Text | Name of the search index |
| `PerformEmbedding` | Boolean | `True` = generate & store vectors via Azure OpenAI; `False` = text-only indexing |
| `AzureOpenAIEndpoint` | Text | Required when `PerformEmbedding = True` |
| `AzureOpenAIKey` | Text | Required when `PerformEmbedding = True` |
| `EmbeddingDeploymentName` | Text | Required when `PerformEmbedding = True` |
| `TenantId` | LongInteger | Tenant isolation key |
| `DocumentId` | LongInteger | Document isolation key |
| `SourceName` | Text | Human-readable label (e.g. `"Report.pdf"`) |
| *(out)* `IsSuccess` | Boolean | |
| *(out)* `Message` | Text | |
| *(out)* `TotalChunksIndexed` | Integer | |

---

### `Delete_Document`
Permanently deletes all chunks for a given `DocumentId` from the index, including any stored embedding vectors.

| Parameter | Type | Description |
|---|---|---|
| `AzureEndpoint` | Text | `https://<name>.search.windows.net` |
| `AzureAdminKey` | Text | Admin key from Azure Portal |
| `AzureIndexName` | Text | Name of the search index |
| `TenantId` | LongInteger | Only chunks matching this tenant are deleted |
| `DocumentId` | LongInteger | All chunks matching both this ID and `TenantId` are deleted |
| *(out)* `IsSuccess` | Boolean | |
| *(out)* `Message` | Text | |
| *(out)* `TotalChunksDeleted` | Integer | |

---

### `Search_Document`
Searches the index and returns the top-N most relevant chunks for the given tenant. Supports vector search (when `PerformEmbedding = True`) or semantic/full-text search (when `False`).

| Parameter | Type | Description |
|---|---|---|
| `AzureEndpoint` | Text | `https://<name>.search.windows.net` |
| `AzureAdminKey` | Text | Admin key from Azure Portal |
| `AzureIndexName` | Text | Name of the search index |
| `PerformEmbedding` | Boolean | `True` = vector search; `False` = semantic/text search |
| `AzureOpenAIEndpoint` | Text | Required when `PerformEmbedding = True` |
| `AzureOpenAIKey` | Text | Required when `PerformEmbedding = True` |
| `EmbeddingDeploymentName` | Text | Required when `PerformEmbedding = True` — must match the model used at indexing time |
| `TenantId` | LongInteger | Only chunks with this `tenant_id` are returned |
| `SearchQuery` | Text | Natural-language question or keyword phrase |
| `ReturnTopNChunks` | Integer | Max chunks to retrieve before score filtering (recommended: 5–10) |
| `MinimumRelevanceScore` | Decimal | Score threshold (default `1.5`). Both text and vector search use the Azure AI semantic reranker score (**0–4** on Basic tier+). Set to `0.0` to disable filtering |
| *(out)* `IsSuccess` | Boolean | |
| *(out)* `Message` | Text | |
| *(out)* `JSON` | Text | JSON array of results (see below) |

#### Search Result JSON Format
```json
[
  {
    "content": "Azure AI Search supports hybrid vector and semantic search...",
    "relevanceScore": 0.8213
  },
  {
    "content": "To create an index, navigate to your Search service in the Azure Portal...",
    "relevanceScore": 0.7541
  }
]
```
Pass `content` values directly to your AI model as RAG context.

> **Score ranges:** This library always applies Azure AI semantic reranking, so `relevanceScore` is the semantic reranker confidence score (**0–4**) for **both** `PerformEmbedding=True` (vector + reranking) and `PerformEmbedding=False` (text + reranking). Score guide: 1.0 = low, 2.0 = moderate, 3.0 = good, 4.0 = maximum. On the Free tier (no semantic search), scores fall back to base BM25/RRF values with no fixed upper bound. Recommended starting threshold: **1.5**.

---

## How It Works

```
PDF Binary
    │
    ▼
PdfChunkerService        — extracts text, splits into sections, creates ≤1000-char chunks
    │
    ▼
EmbeddingService         — calls Azure OpenAI to generate a float[] vector per chunk
(only if PerformEmbedding = True)
    │
    ▼
AzureSearchService       — indexes chunks via Azure AI Search REST API (2024-07-01)
    │
    ▼
Azure AI Search Index    — stores id, tenant_id, document_id, sourceName, section, content, embedding
```

On search, the flow reverses: the query is optionally embedded → sent to Azure AI Search → results filtered by `tenant_id` and `MinimumRelevanceScore` → returned as JSON.

---

## Project Structure

```
├── IAzureAISearch.cs                  # OSInterface — OutSystems contract & parameter descriptions
├── AzureAISearchImpl.cs               # Implementation of all three actions
├── AzureAISearchTutorialCompanion.csproj
├── AssemblyInfo.cs
├── Models/
│   ├── AzureSearchConfig.cs           # Search service connection settings
│   ├── DocumentChunk.cs               # Index document schema
│   └── SearchResultChunk.cs          # Search result + relevance score
├── Services/
│   ├── AzureSearchService.cs          # Azure AI Search REST API calls
│   ├── EmbeddingService.cs            # Azure OpenAI embedding REST API calls
│   └── PdfChunkerService.cs          # PDF text extraction and chunking
├── TestConsole/
│   ├── Program.cs                     # Interactive end-to-end test console
│   └── TestConsole.csproj
├── publish.sh                         # Build & package for ODC upload
└── icon.png
```

---

## About the Author

**Ankit Gangrade** is an Enterprise OutSystems Architect and AI & Low-Code Architect. He is the creator of **Lowcademy**, a learning platform focused on helping developers master Low-Code development through practical tutorials, real-world projects, and hands-on learning.

- 🌐 [ankitg.in](https://ankitg.in)
- 💼 [linkedin.com/in/ankitgangrade](https://linkedin.com/in/ankitgangrade)
- 📺 [youtube.com/@lowcademy](https://youtube.com/@lowcademy)
- 🎓 [lowcademy.com](https://lowcademy.com)

---

## License

This project is intended for educational purposes. Feel free to use and adapt it for your own OutSystems ODC projects.
