using AzureAISearchTutorialCompanion;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Banner("Azure AI Search — End-to-End Test");

// ── Azure AI Search credentials ───────────────────────────────────────────────
string azureEndpoint  = Prompt("Azure AI Search Endpoint (https://<name>.search.windows.net)");
string azureAdminKey  = Prompt("Azure AI Search Admin Key");
string azureIndexName = Prompt("Azure AI Search Index Name");

// ── Embedding ─────────────────────────────────────────────────────────────────
bool performEmbedding = YesNo("Enable embeddings? (requires Azure OpenAI) [y/n]");

string azureOpenAIEndpoint    = string.Empty;
string azureOpenAIKey         = string.Empty;
string embeddingDeployment    = string.Empty;

if (performEmbedding)
{
    azureOpenAIEndpoint  = Prompt("Azure OpenAI Endpoint (https://<name>.openai.azure.com  OR  https://<name>.cognitiveservices.azure.com)");
    azureOpenAIKey       = Prompt("Azure OpenAI Key");

    // List deployments on this resource so the user can confirm the exact deployment name
    Console.WriteLine("\n  Checking available deployments on the endpoint...");
    try
    {
        var embSvc = new AzureAISearchTutorialCompanion.Services.EmbeddingService();
        var deps = embSvc.ListDeployments(azureOpenAIEndpoint, azureOpenAIKey);
        if (deps.Count == 0)
            Console.WriteLine("  (no deployments found — verify endpoint and key)");
        else
            foreach (var (dname, dmodel, dstatus) in deps)
                Console.WriteLine($"    Name: {dname,-40}  Model: {dmodel,-35}  Status: {dstatus}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Could not list deployments: {ex.Message}");
    }
    Console.WriteLine();

    embeddingDeployment  = Prompt("Embedding Deployment Name (copy the exact Name from the list above)");
}

// ── Document identity ─────────────────────────────────────────────────────────
long   tenantId   = PromptLong("Tenant ID (Long Integer)");
long   documentId = PromptLong("Document ID (Long Integer)");
string sourceName = Prompt("Source Name (e.g. TestDocument.pdf)");
string pdfPath    = Prompt("Full path to PDF file");

if (!File.Exists(pdfPath))
{
    Fail($"File not found: {pdfPath}");
    return;
}

byte[] pdfBytes = File.ReadAllBytes(pdfPath);
Console.WriteLine($"\n  PDF loaded: {pdfBytes.Length:N0} bytes\n");

var impl = new AzureAISearchImpl();

// ═══════════════════════════════════════════════════════════════════════════════
// STEP 1 — Index_Document
// ═══════════════════════════════════════════════════════════════════════════════
Banner("STEP 1 — Index_Document");
Console.WriteLine($"  PerformEmbedding : {performEmbedding}");
Console.WriteLine($"  TenantId         : {tenantId}");
Console.WriteLine($"  DocumentId       : {documentId}");
Console.WriteLine($"  SourceName       : {sourceName}");
Console.WriteLine();

impl.Index_Document(
    PDFBinary:              pdfBytes,
    AzureEndpoint:          azureEndpoint,
    AzureAdminKey:          azureAdminKey,
    AzureIndexName:         azureIndexName,
    PerformEmbedding:       performEmbedding,
    AzureOpenAIEndpoint:    azureOpenAIEndpoint,
    AzureOpenAIKey:         azureOpenAIKey,
    EmbeddingDeploymentName: embeddingDeployment,
    TenantId:               tenantId,
    DocumentId:             documentId,
    SourceName:             sourceName,
    IsSuccess:              out bool indexSuccess,
    Message:                out string indexMessage,
    TotalChunksIndexed:     out int totalChunks);

PrintResult("Index_Document", indexSuccess, indexMessage);
if (indexSuccess) Console.WriteLine($"  Chunks indexed : {totalChunks}");

if (!indexSuccess)
{
    Console.WriteLine("\n  Cannot continue — indexing failed.");
    return;
}

// ═══════════════════════════════════════════════════════════════════════════════
// STEP 2 — Search_Document
// ═══════════════════════════════════════════════════════════════════════════════
Banner("STEP 2 — Search_Document");
string searchQuery   = Prompt("Enter search query");
int    topN          = PromptInt("How many top chunks to retrieve?", defaultValue: 5);
double minScore      = PromptDouble("Minimum relevance score (0.0 – 1.0)", defaultValue: 0.55);

impl.Search_Document(
    AzureEndpoint:            azureEndpoint,
    AzureAdminKey:            azureAdminKey,
    AzureIndexName:           azureIndexName,
    PerformEmbedding:         performEmbedding,
    AzureOpenAIEndpoint:      azureOpenAIEndpoint,
    AzureOpenAIKey:           azureOpenAIKey,
    EmbeddingDeploymentName:  embeddingDeployment,
    TenantId:                 tenantId,
    SearchQuery:              searchQuery,
    ReturnTopNChunks:         topN,
    MinimumRelevanceScore:    minScore,
    IsSuccess:                out bool searchSuccess,
    Message:                  out string searchMessage,
    JSON:                     out string json);

PrintResult("Search_Document", searchSuccess, searchMessage);
if (searchSuccess)
{
    Console.WriteLine("\n  ── Results ──────────────────────────────────────────");
    Console.WriteLine(json);
    Console.WriteLine("  ─────────────────────────────────────────────────────");
}

// ═══════════════════════════════════════════════════════════════════════════════
// STEP 3 — Delete_Document
// ═══════════════════════════════════════════════════════════════════════════════
Banner("STEP 3 — Delete_Document");
Console.Write($"  Delete all chunks for Document_Id {documentId}? [y/n]: ");
if (Console.ReadLine()?.Trim().ToLower() != "y")
{
    Console.WriteLine("  Skipped.");
    return;
}

impl.Delete_Document(
    AzureEndpoint:      azureEndpoint,
    AzureAdminKey:      azureAdminKey,
    AzureIndexName:     azureIndexName,
    DocumentId:         documentId,
    IsSuccess:          out bool deleteSuccess,
    Message:            out string deleteMessage,
    TotalChunksDeleted: out int deletedCount);

PrintResult("Delete_Document", deleteSuccess, deleteMessage);
if (deleteSuccess) Console.WriteLine($"  Chunks deleted : {deletedCount}");

// ═══════════════════════════════════════════════════════════════════════════════
// STEP 4 — Verify deletion (search should return empty)
// ═══════════════════════════════════════════════════════════════════════════════
if (deleteSuccess)
{
    Banner("STEP 4 — Verify deletion (re-run same search)");
    impl.Search_Document(
        AzureEndpoint:            azureEndpoint,
        AzureAdminKey:            azureAdminKey,
        AzureIndexName:           azureIndexName,
        PerformEmbedding:         performEmbedding,
        AzureOpenAIEndpoint:      azureOpenAIEndpoint,
        AzureOpenAIKey:           azureOpenAIKey,
        EmbeddingDeploymentName:  embeddingDeployment,
        TenantId:                 tenantId,
        SearchQuery:              searchQuery,
        ReturnTopNChunks:         topN,
        MinimumRelevanceScore:    minScore,
        IsSuccess:                out bool verifySuccess,
        Message:                  out string verifyMessage,
        JSON:                     out string verifyJson);

    PrintResult("Search after delete", verifySuccess, verifyMessage);
    Console.WriteLine($"  Result : {verifyJson}");
    if (verifyJson == "[]")
        Console.WriteLine("  ✓ Empty — delete confirmed.");
    else
        Console.WriteLine("  ⚠ Non-empty result after delete — chunks may still be indexing.");
}

Console.WriteLine("\n  Done.\n");

// ── Helpers ───────────────────────────────────────────────────────────────────

static string Prompt(string label)
{
    Console.Write($"  {label}: ");
    return Console.ReadLine()?.Trim() ?? string.Empty;
}

static bool YesNo(string label)
{
    Console.Write($"  {label}: ");
    return Console.ReadLine()?.Trim().ToLower() == "y";
}

static long PromptLong(string label)
{
    while (true)
    {
        Console.Write($"  {label}: ");
        if (long.TryParse(Console.ReadLine()?.Trim(), out var v)) return v;
        Console.WriteLine("  Please enter a valid integer.");
    }
}

static int PromptInt(string label, int defaultValue)
{
    Console.Write($"  {label} [{defaultValue}]: ");
    var input = Console.ReadLine()?.Trim();
    return int.TryParse(input, out var v) ? v : defaultValue;
}

static double PromptDouble(string label, double defaultValue)
{
    while (true)
    {
        Console.Write($"  {label} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input)) return defaultValue;
        if (double.TryParse(input, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v)) return v;
        Console.WriteLine("  Please enter a decimal number (e.g. 0.55).");
    }
}

static void Banner(string title)
{
    Console.WriteLine();
    Console.WriteLine($"  ════════════════════════════════════════════");
    Console.WriteLine($"  {title}");
    Console.WriteLine($"  ════════════════════════════════════════════");
}

static void PrintResult(string action, bool success, string message)
{
    var icon = success ? "✅" : "❌";
    Console.WriteLine($"\n  {icon} {action}");
    Console.WriteLine($"     {message}");
}

static void Fail(string msg)
{
    Console.WriteLine($"\n  ❌ {msg}");
}
