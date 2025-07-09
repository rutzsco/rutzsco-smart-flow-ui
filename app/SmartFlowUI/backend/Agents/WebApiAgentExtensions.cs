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
        api.MapPut("agent/{agentId}", OnUpdateAgentAsync);
        api.MapDelete("agents/{agentName}", OnDeleteAgentsByNameAsync);

        return app;
    }

    private static async Task<IResult> OnGetAgentsAsync(HttpContext context, AzureAIAgentManagementService service)
    {
        var agents = await service.ListAgentsAsync();
        return Results.Ok(agents);
    }

    #pragma warning disable SKEXP0110
    private static async Task<IResult> OnCreateAgentAsync(AgentViewModel agentViewModel, AzureAIAgentManagementService service, HttpContext context)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(agentViewModel.Name))
        {
            return Results.BadRequest("Agent Name is required.");
        }

        if (string.IsNullOrWhiteSpace(agentViewModel.Instructions))
        {
            return Results.BadRequest("Agent Instructions are required.");
        }

        try
        {
            var createdAgent = await service.CreateAgentAsync(agentViewModel.Name, agentViewModel.Instructions, agentViewModel.Id ?? "gpt-4o");
            
            // Return the created agent information
            var response = new AgentViewModel
            {
                Id = createdAgent.Definition.Id,
                Name = createdAgent.Definition.Name,
                Instructions = createdAgent.Definition.Instructions,
                Description = createdAgent.Definition.Description,
                CreatedAt = createdAgent.Definition.CreatedAt
            };

            return Results.Created($"/api/agents/{response.Id}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            // Log the exception details (not shown here for brevity)
            return Results.Problem($"An error occurred while creating the agent: {ex.Message}");
        }
    }
    #pragma warning restore SKEXP0110

    #pragma warning disable SKEXP0110
    private static async Task<IResult> OnUpdateAgentAsync(string agentId, AgentViewModel agentViewModel, AzureAIAgentManagementService service, HttpContext context)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return Results.BadRequest("Agent ID is required.");
        }

        if (string.IsNullOrWhiteSpace(agentViewModel.Name))
        {
            return Results.BadRequest("Agent Name is required.");
        }

        if (string.IsNullOrWhiteSpace(agentViewModel.Instructions))
        {
            return Results.BadRequest("Agent Instructions are required.");
        }

        try
        {
            var updatedAgent = await service.UpdateAgentAsync(agentId, agentViewModel.Name, agentViewModel.Instructions, agentViewModel.Description, agentViewModel.Id ?? "gpt-4o");
            
            // Return the updated agent information
            var response = new AgentViewModel
            {
                Id = updatedAgent.Definition.Id,
                Name = updatedAgent.Definition.Name,
                Instructions = updatedAgent.Definition.Instructions,
                Description = updatedAgent.Definition.Description,
                CreatedAt = updatedAgent.Definition.CreatedAt
            };

            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            // Log the exception details (not shown here for brevity)
            return Results.Problem($"An error occurred while updating the agent: {ex.Message}");
        }
    }
    #pragma warning restore SKEXP0110

    private static async Task<IResult> OnDeleteAgentsByNameAsync(string agentName, AzureAIAgentManagementService service, HttpContext context)
    {
        try
        {
            var deletedCount = await service.DeleteAgentsByNameAsync(agentName);
            return Results.Ok(new { DeletedCount = deletedCount, Message = $"Deleted {deletedCount} agent(s) with name '{agentName}'" });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return Results.Problem($"An error occurred while deleting agents: {ex.Message}");
        }
    }
}

