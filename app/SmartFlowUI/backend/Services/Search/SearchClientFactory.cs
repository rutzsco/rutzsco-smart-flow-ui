// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;

namespace MinimalApi.Services.Search;

public class SearchClientFactory
{
    private readonly AppConfiguration _configuration;
    private readonly ConcurrentDictionary<string,SearchClient> _clients = new ConcurrentDictionary<string, SearchClient>();
    private readonly TokenCredential? _credential;
    private readonly AzureKeyCredential? _keyCredential;
    private readonly SearchIndexerClient _searchIndexerClient;

    public SearchClientFactory(AppConfiguration configuration, TokenCredential? credential, AzureKeyCredential? keyCredential = null)
    {
        _configuration = configuration;
        _credential = credential;
        _keyCredential = keyCredential;
        _searchIndexerClient = CreateSearchIndexerClient();
    }

    public SearchClient GetOrCreateClient(string indexName)
    {
        // Check if a client for the given index already exists
        if (_clients.TryGetValue(indexName, out var client))
        {
            return client;
        }

        // Create a new client for the index
        var newClient = CreateClientForIndex(indexName);
        _clients[indexName] = newClient;
        return newClient;
    }

    private SearchClient CreateClientForIndex(string indexName)
    {
        if (_keyCredential != null)
        {
            return new SearchClient(new Uri(_configuration.AzureSearchServiceEndpoint), indexName, _keyCredential);
        }
        return new SearchClient(new Uri(_configuration.AzureSearchServiceEndpoint), indexName, _credential);
    }

    public SearchIndexerClient GetSearchIndexerClient()
    {
        return _searchIndexerClient;
    }

    private SearchIndexerClient CreateSearchIndexerClient()
    {
        if (_keyCredential != null)
        {
            return new SearchIndexerClient(new Uri(_configuration.AzureSearchServiceEndpoint), _keyCredential);
        }
        return new SearchIndexerClient(new Uri(_configuration.AzureSearchServiceEndpoint), _credential);
    }
}
