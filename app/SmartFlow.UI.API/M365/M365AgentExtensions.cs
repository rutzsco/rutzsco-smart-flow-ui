// Copyright (c) Microsoft. All rights reserved.

// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using MinimalApi.Agents;
using System.Text.Json;
using System.Security.Claims;
using AgentActivity = Microsoft.Agents.Core.Models.Activity;

namespace MinimalApi.M365;

/// <summary>
/// Simple in-memory storage implementation for the agent
/// </summary>
public class SimpleMemoryStorage : IStorage
{
    private readonly Dictionary<string, object> _storage = new();

    public Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            _storage.Remove(key);
        }
        return Task.CompletedTask;
    }

    public Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, object>();
        foreach (var key in keys)
        {
            if (_storage.TryGetValue(key, out var value))
            {
                result[key] = value;
            }
        }
        return Task.FromResult<IDictionary<string, object>>(result);
    }

    public Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default) where TStoreItem : class
    {
        var result = new Dictionary<string, TStoreItem>();
        foreach (var key in keys)
        {
            if (_storage.TryGetValue(key, out var value) && value is TStoreItem typedValue)
            {
                result[key] = typedValue;
            }
        }
        return Task.FromResult<IDictionary<string, TStoreItem>>(result);
    }

    public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken = default)
    {
        foreach (var change in changes)
        {
            _storage[change.Key] = change.Value;
        }
        return Task.CompletedTask;
    }

    public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class
    {
        foreach (var change in changes)
        {
            _storage[change.Key] = change.Value;
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Simple HTTP adapter implementation for Bot Framework
/// </summary>
public class SimpleAgentHttpAdapter : IAgentHttpAdapter
{
    private readonly ILogger<SimpleAgentHttpAdapter> _logger;

    public SimpleAgentHttpAdapter(ILogger<SimpleAgentHttpAdapter> logger)
    {
        _logger = logger;
    }

    public async Task ProcessAsync(HttpRequest request, HttpResponse response, IAgent agent, CancellationToken cancellationToken = default)
    {
        try
        {
            // Read the activity from the request body
            var activity = await JsonSerializer.DeserializeAsync<AgentActivity>(request.Body, cancellationToken: cancellationToken);
            
            if (activity == null)
            {
                response.StatusCode = 400;
                await response.WriteAsJsonAsync(new { error = "Invalid activity" }, cancellationToken);
                return;
            }

            // Create a turn context
            var turnContext = new SimpleTurnContext(activity);
            
            // Process the activity
            await agent.OnTurnAsync(turnContext, cancellationToken);
            
            // Return the response activity if any
            if (turnContext.SentActivities.Any())
            {
                response.StatusCode = 200;
                await response.WriteAsJsonAsync(turnContext.SentActivities.Last(), cancellationToken);
            }
            else
            {
                response.StatusCode = 200;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing activity");
            response.StatusCode = 500;
            await response.WriteAsJsonAsync(new { error = ex.Message }, cancellationToken);
        }
    }
}

/// <summary>
/// Simple turn context implementation
/// </summary>
public class SimpleTurnContext : ITurnContext
{
    public IActivity Activity { get; }
    public List<IActivity> SentActivities { get; } = new();
    public IChannelAdapter Adapter => throw new NotImplementedException("Adapter not implemented");
    public TurnContextStateCollection Services => throw new NotImplementedException("Services not implemented");
    public TurnContextStateCollection StackState => throw new NotImplementedException("StackState not implemented");
    public IStreamingResponse StreamingResponse { get; set; } = null!;
    public ClaimsIdentity Identity { get; set; } = null!;

    public SimpleTurnContext(IActivity activity)
    {
        Activity = activity;
    }

    public Task<ResourceResponse> SendActivityAsync(string textReplyToSend, string? speak = null, string? inputHint = null, CancellationToken cancellationToken = default)
    {
        var activity = new AgentActivity
        {
            Type = ActivityTypes.Message,
            Text = textReplyToSend
        };
        SentActivities.Add(activity);
        return Task.FromResult(new ResourceResponse { Id = Guid.NewGuid().ToString() });
    }

    public Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
    {
        SentActivities.Add(activity);
        return Task.FromResult(new ResourceResponse { Id = Guid.NewGuid().ToString() });
    }

    public Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
    {
        SentActivities.AddRange(activities);
        var responses = activities.Select(_ => new ResourceResponse { Id = Guid.NewGuid().ToString() }).ToArray();
        return Task.FromResult(responses);
    }

    public Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResourceResponse { Id = Guid.NewGuid().ToString() });
    }

    public Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<ResourceResponse> TraceActivityAsync(string name, object value, string valueType, string label, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResourceResponse { Id = Guid.NewGuid().ToString() });
    }

    public ITurnContext OnSendActivities(SendActivitiesHandler handler)
    {
        return this;
    }

    public ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
    {
        return this;
    }

    public ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
    {
        return this;
    }

    public TurnContextStateCollection TurnState { get; } = new TurnContextStateCollection();
    public bool Responded { get; set; }
}

/// <summary>
/// Extension methods for configuring M365 Agent integration.
/// </summary>
public static class M365AgentExtensions
{
    /// <summary>
    /// Adds M365 Agent services to the service collection.
    /// </summary>
    public static IServiceCollection AddM365AgentServices(
        this IServiceCollection services)
    {
        // Add AgentApplicationOptions required by AgentApplication base class
        // Using SimpleMemoryStorage for state management (can be replaced with persistent storage)
        services.AddSingleton(sp => new AgentApplicationOptions(new SimpleMemoryStorage()));

        // Add the M365AgentAdapter as the IAgent implementation
        services.AddSingleton<IAgent, M365AgentAdapter>();
        
        // Add our simple HTTP adapter implementation
        services.AddSingleton<IAgentHttpAdapter, SimpleAgentHttpAdapter>();

        return services;
    }

    /// <summary>
    /// Maps M365 Agent endpoints to the application.
    /// </summary>
    public static WebApplication MapM365AgentEndpoints(
        this WebApplication app)
    {
        // Map the agent adapter to handle incoming requests
        // The /api/messages endpoint will receive messages from M365 Copilot/Teams
        var incomingRoute = app.MapPost("/api/messages", async (
            HttpRequest request,
            HttpResponse response,
            IAgentHttpAdapter adapter,
            IAgent agent,
            CancellationToken cancellationToken) =>
        {
            await adapter.ProcessAsync(request, response, agent, cancellationToken);
        });

        return app;
    }
}
