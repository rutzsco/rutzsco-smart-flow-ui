// Copyright (c) Microsoft. All rights reserved.
using MinimalApi.Agents;
using MinimalApi.Models;

namespace MinimalApi.Extensions;

internal static class WebApiAgentExtensions
{
    internal static WebApplication MapAgentManagementApi(this WebApplication app)
    {
        var api = app.MapGroup("api");

        // Process chat turn
        api.MapGet("agents", OnGetAgentsAsync);
        api.MapPost("agent", OnCreateAgentAsync);

        return app;
    }

    private static async Task<IResult> OnGetAgentsAsync(HttpContext context, AzureAIAgentManagementService service)
    {
        var agents = await service.ListAgentsAsync();
        return Results.Ok(agents);
    }

    private static async Task<IResult> OnCreateAgentAsync(AgentViewModel agentViewModel, AzureAIAgentManagementService service, HttpContext context)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(agentViewModel.Name) || string.IsNullOrWhiteSpace(agentViewModel.Id))
        {
            return Results.BadRequest("Agent Name and Model ID are required.");
        }

        try
        {
            //var createdAgent = await service.CreateAgentAsync(agentViewModel);
            //return Results.Created($"/api/agents/{createdAgent.Id}", createdAgent);

            return Results.Ok("Agent creation logic is not implemented yet.");
        }
        catch (Exception ex)
        {
            // Log the exception details (not shown here for brevity)
            return Results.Problem($"An error occurred while creating the agent: {ex.Message}");
        }
    }
}

