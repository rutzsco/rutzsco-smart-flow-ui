using System.ComponentModel;
using MinimalApi.Services.Search.IndexDefinitions;

namespace Assistants.Hub.API.Assistants.RAG;

public class RAGRetrivalPlugins
{
    private readonly SearchClientFactory _searchClientFactory;
    private readonly AzureOpenAIClient _azureOpenAIClient;

    public RAGRetrivalPlugins(SearchClientFactory searchClientFactory, AzureOpenAIClient azureOpenAIClient)
    {
        _searchClientFactory = searchClientFactory;
        _azureOpenAIClient = azureOpenAIClient;
    }

    [KernelFunction("get_sources")]
    [Description("Gets relevant information based on the provided search term.")]
    [return: Description("A list relevant source information based on the provided search term.")]
    public async Task<IEnumerable<KnowledgeSource>> GetKnowledgeSourcesAsync(Kernel kernel, [Description("Search query")] string searchQuery)
    {
        try
        {
            var settings = kernel.Data["VectorSearchSettings"] as VectorSearchSettings;
            if (settings == null)
                throw new ArgumentNullException(nameof(settings), "VectorSearchSettings cannot be null");

            // Get the appropriate search function based on the index schema definition
            var searchLogic = GetSearchLogic(settings);
            var results = await searchLogic(searchQuery);

            // Add kernel context for diagnostics
            kernel.AddFunctionCallResult("get_knowledge_articles",  $"Search Query: {searchQuery} /n {System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}", results.ToList());

            return results;
        }
        catch (Exception ex)
        {
            // Log the exception
            kernel.AddFunctionCallResult("get_knowledge_articles", $"Error: {ex.Message}", null);
            throw;
        }
    }

    private Func<string, Task<IEnumerable<KnowledgeSource>>> GetSearchLogic(VectorSearchSettings settings)
    {
        switch (settings.IndexSchemaDefinition)
        {
            case "KwiecienV2":
                var kwiecienLogic = new SearchLogic<KwiecienCustomIndexDefinitionV2>(
                    _azureOpenAIClient, 
                    _searchClientFactory, 
                    KwiecienCustomIndexDefinitionV2.SelectFieldNames, 
                    KwiecienCustomIndexDefinitionV2.EmbeddingsFieldName, 
                    settings);
                return async (query) => await kwiecienLogic.SearchAsync(query);

            case "AISearchV1":
                var aiSearchLogic = new SearchLogic<AISearchIndexerIndexDefinintion>(
                    _azureOpenAIClient, 
                    _searchClientFactory, 
                    AISearchIndexerIndexDefinintion.SelectFieldNames, 
                    AISearchIndexerIndexDefinintion.EmbeddingsFieldName, 
                    settings);
                return async (query) => await aiSearchLogic.SearchAsync(query);

            default:
                throw new ArgumentException($"Unsupported IndexSchemaDefinition: {settings.IndexSchemaDefinition}", 
                    nameof(settings.IndexSchemaDefinition));
        }
    }
}
