using Azure.AI.Agents;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.AzureAI;
using System.Reflection;

namespace MinimalApi.Agents
{        
    #pragma warning disable SKEXP0110
    public class AzureAIAgentBuilder
    { 

        private readonly Kernel _kernel;
        private readonly IConfiguration _configuration;


        public AzureAIAgentBuilder(Kernel kernel, IConfiguration configuration)
        {    
            _configuration = configuration;
            _kernel = kernel;
        }

        public async Task<AzureAIAgent> CreateAgentIfNotExistsAsync()
        {
            var agentsClient = AzureAIAgent.CreateAgentsClient(_configuration["AzureAIFoundryProjectEndpoint"], new DefaultAzureCredential());


            var tools = new List<FunctionToolDefinition>();
            foreach (var plugin in _kernel.Plugins)
            {
                var pluginTools = plugin.Select(f => f.ToToolDefinition(plugin.Name));
                tools.AddRange(pluginTools);
            }


            var definition = await agentsClient.Administration.CreateAgentAsync(
                "gpt-4o",
                name: "rutzsco-chat-agent",
                instructions: LoadEmbeddedResource("MinimalApi.Services.Profile.Prompts.RAGChatSystemPrompt.txt"),
                tools: tools);

            AzureAIAgent agent = new(definition, agentsClient, plugins: _kernel.Plugins);

            return agent;
        }

        private string LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();      
        }
    }

    #pragma warning disable SKEXP0110
}
