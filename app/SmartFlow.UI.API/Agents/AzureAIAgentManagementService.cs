using Azure.AI.Agents;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Shared.Models;
using System.Reflection;

namespace MinimalApi.Agents
{        
    #pragma warning disable SKEXP0110
    public class AzureAIAgentManagementService : IAgentManagementService
    { 

        private readonly OpenAIClientFacade _openAIClientFacade;
        private readonly IConfiguration _configuration;

        public string ProviderType => "AzureAI";

        public AzureAIAgentManagementService(OpenAIClientFacade openAIClientFacade, IConfiguration configuration)
        {    
            _configuration = configuration;
            _openAIClientFacade = openAIClientFacade;
        }

        public async Task<AzureAIAgent> CreateAgentIfNotExistsAsync()
        {
            var agentsClient = AzureAIAgent.CreateAgentsClient(_configuration["AzureAIFoundryProjectEndpoint"], new DefaultAzureCredential());
            var kernel = _openAIClientFacade.BuildKernel("RAG");

            var tools = new List<FunctionToolDefinition>();
            foreach (var plugin in kernel.Plugins)
            {
                var pluginTools = plugin.Select(f => f.ToToolDefinition(plugin.Name));
                tools.AddRange(pluginTools);
            }


            var definition = await agentsClient.Administration.CreateAgentAsync(
                "gpt-4o",
                name: "rutzsco-chat-agent",
                instructions: LoadEmbeddedResource("MinimalApi.Services.Profile.Prompts.RAGChatSystemPrompt.txt"),
                tools: tools);

            AzureAIAgent agent = new(definition, agentsClient, plugins: kernel.Plugins);

            return agent;
        }

        public async Task<AgentViewModel> GetAgentAsync(string agentId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                throw new ArgumentException("Agent ID cannot be null or empty.", nameof(agentId));
            }

            var agentsClient = AzureAIAgent.CreateAgentsClient(_configuration["AzureAIFoundryProjectEndpoint"], new DefaultAzureCredential());
            var agentDefinition = await agentsClient.Administration.GetAgentAsync(agentId);
            
            return ConvertToAgentViewModel(agentDefinition.Value);
        }

        public async Task<AgentViewModel> CreateAgentAsync(string name, string instructions, string? description = null, string model = "gpt-4o", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Agent name cannot be null or empty.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(instructions))
            {
                throw new ArgumentException("Agent instructions cannot be null or empty.", nameof(instructions));
            }

            var agentsClient = AzureAIAgent.CreateAgentsClient(_configuration["AzureAIFoundryProjectEndpoint"], new DefaultAzureCredential());
            var kernel = _openAIClientFacade.BuildKernel("RAG");

            var tools = new List<FunctionToolDefinition>();
            foreach (var plugin in kernel.Plugins)
            {
                var pluginTools = plugin.Select(f => f.ToToolDefinition(plugin.Name));
                tools.AddRange(pluginTools);
            }

            var definition = await agentsClient.Administration.CreateAgentAsync(
                model,
                name: name,
                instructions: instructions,
                description: description,
                tools: tools);

            return ConvertToAgentViewModel(definition);
        }

        public async Task<AgentViewModel> UpdateAgentAsync(string agentId, string name, string instructions, string? description = null, string model = "gpt-4o", CancellationToken cancellationToken = default)
        {
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

            var agentsClient = AzureAIAgent.CreateAgentsClient(_configuration["AzureAIFoundryProjectEndpoint"], new DefaultAzureCredential());
            var kernel = _openAIClientFacade.BuildKernel("RAG");

            var tools = new List<FunctionToolDefinition>();
            foreach (var plugin in kernel.Plugins)
            {
                var pluginTools = plugin.Select(f => f.ToToolDefinition(plugin.Name));
                tools.AddRange(pluginTools);
            }

            var definition = await agentsClient.Administration.UpdateAgentAsync(
                agentId,
                model: model,
                name: name,
                instructions: instructions,
                description: description,
                tools: tools);

            return ConvertToAgentViewModel(definition);
        }

        public async Task<IEnumerable<AgentViewModel>> ListAgentsAsync(CancellationToken cancellationToken = default)
        {
            var agentsClient = AzureAIAgent.CreateAgentsClient(_configuration["AzureAIFoundryProjectEndpoint"], new DefaultAzureCredential());
            var agents = new List<AgentViewModel>();
            await foreach (var agentDefinition in agentsClient.Administration.GetAgentsAsync())
            {
                agents.Add(ConvertToAgentViewModel(agentDefinition));
            }
            return agents;
        }

        public async Task<int> DeleteAgentsByNameAsync(string agentName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(agentName))
            {
                throw new ArgumentException("Agent name cannot be null or empty.", nameof(agentName));
            }

            var agentsClient = AzureAIAgent.CreateAgentsClient(_configuration["AzureAIFoundryProjectEndpoint"], new DefaultAzureCredential());
            var deletedCount = 0;

            // Get all agents and find those that match the name
            var agentsToDelete = new List<PersistentAgent>();
            await foreach (var agentDefinition in agentsClient.Administration.GetAgentsAsync())
            {
                if (string.Equals(agentDefinition.Name, agentName, StringComparison.OrdinalIgnoreCase))
                {
                    agentsToDelete.Add(agentDefinition);
                }
            }

            // Delete each matching agent
            foreach (var agent in agentsToDelete)
            {
                try
                {
                    await agentsClient.Administration.DeleteAgentAsync(agent.Id);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    // Log the exception or handle it as needed
                    // For now, we'll continue with other agents
                    Console.WriteLine($"Failed to delete agent {agent.Id} ({agent.Name}): {ex.Message}");
                }
            }

            return deletedCount;
        }

        private AgentViewModel ConvertToAgentViewModel(PersistentAgent agent)
        {
            return new AgentViewModel
            {
                Id = agent.Id,
                Name = agent.Name,
                Instructions = agent.Instructions,
                Description = agent.Description,
                Model = agent.Model,
                CreatedAt = agent.CreatedAt.DateTime,
                Tools = agent.Tools?.Select(t => t.GetType().Name).ToList() ?? new List<string>()
            };
        }

        private string LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();      
        }
    }

    #pragma warning restore SKEXP0110
}
