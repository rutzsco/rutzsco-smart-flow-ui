// Copyright (c) Microsoft. All rights reserved.

using AgentActivity = Microsoft.Agents.Core.Models.Activity;
using MinimalApi.Agents;

namespace MinimalApi.M365;

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
        // Add the M365AgentAdapter as a transient service
        services.AddTransient<M365AgentAdapter>();

        return services;
    }

    /// <summary>
    /// Maps M365 Agent endpoints to the application.
    /// </summary>
    public static WebApplication MapM365AgentEndpoints(
        this WebApplication app)
    {
        // Map the agent adapter to handle incoming requests
        // The /api/m365/messages endpoint will receive messages from M365 Copilot/Teams
        app.MapPost("/api/m365/messages", async (
            AgentActivity activity,
            M365AgentAdapter adapter,
            CancellationToken cancellationToken) =>
        {
            var response = await adapter.ProcessActivityAsync(activity, cancellationToken);
            return Results.Ok(response);
        });

        return app;
    }
}
