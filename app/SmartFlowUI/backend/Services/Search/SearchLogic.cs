// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using TiktokenSharp;

namespace MinimalApi.Services.Search;

public class SearchLogic<T> where T : IKnowledgeSource
{
    private readonly SearchClient _searchClient;
    private readonly AzureOpenAIClient _openAIClient;
    private readonly List<string> _selectFields;
    private readonly VectorSearchSettings _settings;
    private readonly string _embeddingFieldName;

    public SearchLogic(AzureOpenAIClient openAIClient, SearchClientFactory factory, List<string> selectFields, string embeddingFieldName, VectorSearchSettings settings)
    {
        _searchClient = factory.GetOrCreateClient(settings.IndexName);
        _openAIClient = openAIClient;
        _selectFields = selectFields;
        _settings = settings;
        _embeddingFieldName = embeddingFieldName;
    }

    public async Task<List<KnowledgeSource>> SearchAsync(string query)
    {
        // Generate the embedding for the query
        var queryEmbeddings = await GenerateEmbeddingsAsync(query, _openAIClient);


        var searchOptions = new SearchOptions
        {
            Size = _settings.DocumentCount,
            VectorSearch = new()
            {
                Queries = { new VectorizedQuery(queryEmbeddings.ToArray()) { KNearestNeighborsCount = _settings.KNearestNeighborsCount, Fields = { _embeddingFieldName }, Exhaustive = _settings.Exhaustive } }
            }
        };
      
        if (_settings.UseSemanticRanker)
        {
            searchOptions.SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = _settings.SemanticConfigurationName
            };
            searchOptions.QueryType = SearchQueryType.Semantic;
        }

        foreach (var field in _selectFields)
        {
            searchOptions.Select.Add(field);
        }

        // Perform the search and build the results
        var response = await _searchClient.SearchAsync<T>(query, searchOptions);
        var list = new List<T>();
        foreach (var result in response.Value.GetResults())
        {
            list.Add(result.Document);
        }

        // Filter the results by the maximum request token size
        var sources = FilterByMaxRequestTokenSize(list, _settings.MaxSourceTokens, _settings.CitationUseSourcePage);
        return sources.ToList();
    }

    private IEnumerable<KnowledgeSource> FilterByMaxRequestTokenSize(IReadOnlyList<T> sources, int maxRequestTokens, bool citationUseSourcePage)
    {
        int sourceSize = 0;
        int tokenSize = 0;
        var documents = new List<IKnowledgeSource>();
        var tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");
        foreach (var document in sources)
        {
            var text = document.GetSource().Content;
            sourceSize += text.Length;
            tokenSize += tikToken.Encode(text).Count;
            if (tokenSize > maxRequestTokens)
            {
                break;
            }
            yield return document.GetSource(citationUseSourcePage);
        }
    }

    private async Task<ReadOnlyMemory<float>> GenerateEmbeddingsAsync(string text, AzureOpenAIClient openAIClient)
    {
        var response = await openAIClient.GetEmbeddingClient(_settings.EmbeddingModelName).GenerateEmbeddingsAsync(new List<string>{ text });
        return response.Value[0].ToFloats();
    }
}
