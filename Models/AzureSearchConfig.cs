namespace AzureAISearchTutorialCompanion.Models;

internal class AzureSearchConfig
{
    public string Endpoint        { get; set; } = string.Empty;
    public string PrimaryAdminKey { get; set; } = string.Empty;
    public string QueryKey        { get; set; } = string.Empty;
    public string IndexName       { get; set; } = string.Empty;
}
