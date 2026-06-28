using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureAISearchTutorialCompanion.Services;

internal class EmbeddingService
{
    private const string ApiVersion = "2024-10-21";

    public float[] GetEmbedding(string text, string endpoint, string apiKey, string deploymentName)
    {
        var url     = $"{endpoint.TrimEnd('/')}/openai/deployments/{deploymentName}/embeddings?api-version={ApiVersion}";
        var payload = JsonSerializer.Serialize(new { input = text });

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.Add("api-key", apiKey);

        var content  = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.ContentType!.CharSet = null; // send: Content-Type: application/json  (no charset, matches curl)
        var response = client.PostAsync(url, content).GetAwaiter().GetResult();
        var body     = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure OpenAI embedding error (HTTP {(int)response.StatusCode}): {body}");

        var parsed = JsonSerializer.Deserialize<EmbeddingResponse>(body);
        var vector = parsed?.Data?.FirstOrDefault()?.Embedding;

        if (vector == null || vector.Length == 0)
            throw new InvalidOperationException("Azure OpenAI returned an empty embedding vector.");

        return vector;
    }

    public List<(string Name, string Model, string Status)> ListDeployments(string endpoint, string apiKey)
    {
        var url = $"{endpoint.TrimEnd('/')}/openai/deployments?api-version={ApiVersion}";
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.Add("api-key", apiKey);
        var response = client.GetAsync(url).GetAwaiter().GetResult();
        var body     = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to list deployments (HTTP {(int)response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var result = new List<(string, string, string)>();
        if (doc.RootElement.TryGetProperty("value", out var arr))
            foreach (var item in arr.EnumerateArray())
            {
                var name   = item.TryGetProperty("id",     out var n) ? n.GetString() ?? "" : "";
                var model  = item.TryGetProperty("model",  out var m) ? m.GetString() ?? "" : "";
                var status = item.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                result.Add((name, model, status));
            }
        return result;
    }

    private class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingItem>? Data { get; set; }
    }

    private class EmbeddingItem
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
