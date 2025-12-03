// Copyright (c) Microsoft. All rights reserved.

using Shared.Models;
using System.Text;
using System.Text.Json;

namespace MinimalApi.Agents;

/// <summary>
/// Agent management service that communicates with a custom REST endpoint
/// </summary>
public class CustomEndpointAgentManagementService : IAgentManagementService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CustomEndpointAgentManagementService> _logger;
    private readonly string _endpointUrl;
    private readonly string? _apiKey;

    public string ProviderType => "CustomEndpoint";

    public CustomEndpointAgentManagementService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CustomEndpointAgentManagementService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;

        // Load endpoint configuration
        _endpointUrl = _configuration["CustomAgentEndpoint"] ?? string.Empty;
        _apiKey = _configuration["CustomAgentApiKey"];

        if (string.IsNullOrWhiteSpace(_endpointUrl))
        {
            _logger.LogWarning("CustomAgentEndpoint is not configured. Custom endpoint agent management will not be available.");
        }
    }

    public async Task<IEnumerable<AgentViewModel>> ListAgentsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_endpointUrl))
        {
            throw new InvalidOperationException("CustomAgentEndpoint is not configured.");
        }

        try
        {
            using var httpClient = CreateHttpClient();
            var response = await httpClient.GetAsync($"{_endpointUrl}/Agents", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var agents = JsonSerializer.Deserialize<List<AgentViewModel>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return agents ?? new List<AgentViewModel>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to list agents from custom endpoint: {Endpoint}", _endpointUrl);
            throw new InvalidOperationException($"Failed to communicate with custom agent endpoint: {ex.Message}", ex);
        }
    }

    public async Task<AgentViewModel> CreateAgentAsync(
        string name,
        string instructions,
        string? description = null,
        string model = "gpt-4o",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_endpointUrl))
        {
            throw new InvalidOperationException("CustomAgentEndpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Agent name cannot be null or empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw new ArgumentException("Agent instructions cannot be null or empty.", nameof(instructions));
        }

        try
        {
            using var httpClient = CreateHttpClient();
            
            var agentData = new
            {
                name = name,
                instructions = instructions,
                description = description,
                model = model
            };

            var jsonContent = JsonSerializer.Serialize(agentData);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{_endpointUrl}/Agent", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var agent = JsonSerializer.Deserialize<AgentViewModel>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (agent == null)
            {
                throw new InvalidOperationException("Failed to deserialize agent response from custom endpoint.");
            }

            return agent;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to create agent at custom endpoint: {Endpoint}", _endpointUrl);
            throw new InvalidOperationException($"Failed to communicate with custom agent endpoint: {ex.Message}", ex);
        }
    }

    public async Task<AgentViewModel> UpdateAgentAsync(
        string agentId,
        string name,
        string instructions,
        string? description = null,
        string model = "gpt-4o",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_endpointUrl))
        {
            throw new InvalidOperationException("CustomAgentEndpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID cannot be null or empty.", nameof(agentId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Agent name cannot be null or empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw new ArgumentException("Agent instructions cannot be null or empty.", nameof(instructions));
        }

        try
        {
            using var httpClient = CreateHttpClient();
            
            var agentData = new
            {
                name = name,
                instructions = instructions,
                description = description,
                model = model
            };

            var jsonContent = JsonSerializer.Serialize(agentData);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PutAsync($"{_endpointUrl}/Agent/{agentId}", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var agent = JsonSerializer.Deserialize<AgentViewModel>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (agent == null)
            {
                throw new InvalidOperationException("Failed to deserialize agent response from custom endpoint.");
            }

            return agent;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to update agent at custom endpoint: {Endpoint}", _endpointUrl);
            throw new InvalidOperationException($"Failed to communicate with custom agent endpoint: {ex.Message}", ex);
        }
    }

    public async Task<int> DeleteAgentsByNameAsync(string agentName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_endpointUrl))
        {
            throw new InvalidOperationException("CustomAgentEndpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentException("Agent name cannot be null or empty.", nameof(agentName));
        }

        try
        {
            using var httpClient = CreateHttpClient();
            var response = await httpClient.DeleteAsync($"{_endpointUrl}/Agents/{Uri.EscapeDataString(agentName)}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<DeleteAgentsResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.DeletedCount ?? 0;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to delete agents from custom endpoint: {Endpoint}", _endpointUrl);
            throw new InvalidOperationException($"Failed to communicate with custom agent endpoint: {ex.Message}", ex);
        }
    }

    private HttpClient CreateHttpClient()
    {
        var httpClient = _httpClientFactory.CreateClient();
        
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }

        return httpClient;
    }

    private class DeleteAgentsResponse
    {
        public int DeletedCount { get; set; }
        public string? Message { get; set; }
    }
}
