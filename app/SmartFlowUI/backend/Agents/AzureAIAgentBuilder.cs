using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.AzureAI;
using System.Reflection;

namespace QNDispositionAgent.Logic
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
            AIProjectClient client = AzureAIAgent.CreateAzureAIClient(_configuration["AzureProjectConnectionString"], new DefaultAzureCredential());
            AgentsClient agentsClient = client.GetAgentsClient();

            var tools = new List<FunctionToolDefinition>();
            foreach (var plugin in _kernel.Plugins)
            {
                var pluginTools = plugin.Select(f => f.ToToolDefinition(plugin.Name));
                tools.AddRange(pluginTools);
            }
   
            
            Azure.AI.Projects.Agent definition = await agentsClient.CreateAgentAsync(
                "gpt-4o",
                name: "rutzsco-txtav-disposition-generation-agent",
                instructions: LoadEmbeddedResource("MinimalApi.Services.Profile.Prompts.QualityRemedyAgentInstructions.txt"),
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
