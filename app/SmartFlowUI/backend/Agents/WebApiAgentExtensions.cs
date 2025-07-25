﻿// Copyright (c) Microsoft. All rights reserved.
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
        var agentViewModels = agents.Select(agent => new AgentViewModel
        {
            Id = agent.Id,
            Name = agent.Name,
            Instructions = agent.Instructions,
            Description = agent.Description,
            Model = agent.Model,
            CreatedAt = agent.CreatedAt
        });
        return Results.Ok(agentViewModels);
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
            var model = !string.IsNullOrWhiteSpace(agentViewModel.Model) ? agentViewModel.Model : "gpt-4.1";
            var createdAgent = await service.CreateAgentAsync(agentViewModel.Name, agentViewModel.Instructions, model);
            
            // Return the created agent information
            var response = new AgentViewModel
            {
                Id = createdAgent.Definition.Id,
                Name = createdAgent.Definition.Name,
                Instructions = createdAgent.Definition.Instructions,
                Description = createdAgent.Definition.Description,
                Model = createdAgent.Definition.Model,
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
            var model = !string.IsNullOrWhiteSpace(agentViewModel.Model) ? agentViewModel.Model : "gpt-4o";
            var updatedAgent = await service.UpdateAgentAsync(agentId, agentViewModel.Name, agentViewModel.Instructions, agentViewModel.Description, model);
            
            // Return the updated agent information
            var response = new AgentViewModel
            {
                Id = updatedAgent.Definition.Id,
                Name = updatedAgent.Definition.Name,
                Instructions = updatedAgent.Definition.Instructions,
                Description = updatedAgent.Definition.Description,
                Model = updatedAgent.Definition.Model,
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

