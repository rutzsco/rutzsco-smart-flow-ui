// Copyright (c) Microsoft. All rights reserved.

using Azure.Core;
using Azure.Identity;
using MinimalApi.Agents;
using MinimalApi.Services.Profile;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace MinimalApi.Services;

internal sealed class EndpointChatService : IChatService
{
    private readonly ILogger<EndpointChatService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly TokenCredential _tokenCredential;

    public EndpointChatService(ILogger<EndpointChatService> logger, HttpClient httpClient, IConfiguration configuration, TokenCredential tokenCredential)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
        _tokenCredential = tokenCredential;
    }


    public async IAsyncEnumerable<ChatChunkResponse> ReplyAsync(UserInformation user, ProfileDefinition profile, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(request.History);

        using var apiRequest = new HttpRequestMessage(HttpMethod.Post, _configuration[profile.AssistantEndpointSettings.APIEndpointSetting]);
        
        // Get access token from managed identity
        // Use configurable scope, defaulting to a common Azure scope if not specified
        var scope = _configuration["EndpointTokenScope"] ?? "https://cognitiveservices.azure.com/.default";
        var tokenRequestContext = new TokenRequestContext(new[] { scope });
        var accessToken = await _tokenCredential.GetTokenAsync(tokenRequestContext, cancellationToken);
        
        // Use bearer token instead of API key
        apiRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
        apiRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(apiRequest, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (ChatChunkResponse chunk in System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<ChatChunkResponse>(responseStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, DefaultBufferSize = 32 }))
        {
            if (chunk == null)
                continue;

            yield return chunk;
            await Task.Yield();
        }

    }
}
