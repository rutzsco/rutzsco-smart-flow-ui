// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.Projects;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MinimalApi.Agents;

#pragma warning disable SKEXP0110
public class AzureAIAgentChatService : IChatService
{
    private readonly ILogger<AzureAIAgentChatService> _logger;
    private readonly IConfiguration _configuration;
    private readonly OpenAIClientFacade _openAIClientFacade;
    private readonly AgentsClient _agentsClient;
    public AzureAIAgentChatService(OpenAIClientFacade openAIClientFacade, AzureBlobStorageService blobStorageService, ILogger<AzureAIAgentChatService> logger, IConfiguration configuration)
    {
        _openAIClientFacade = openAIClientFacade;
        _logger = logger;
        _configuration = configuration;

        var azureProjectConnectionString = _configuration["AzureProjectConnectionString"];
        ArgumentNullException.ThrowIfNullOrEmpty(azureProjectConnectionString, "AzureProjectConnectionString");
        var client = AzureAIAgent.CreateAzureAIClient(azureProjectConnectionString, new DefaultAzureCredential());
        _agentsClient = client.GetAgentsClient();
    }

    public async IAsyncEnumerable<ChatChunkResponse> ReplyAsync(UserInformation user, ProfileDefinition profile, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var sb = new StringBuilder();
        var userMessage = request.LastUserQuestion;

        var kernel = _openAIClientFacade.BuildKernel(string.Empty);
        var definition = _agentsClient.GetAgent(profile.AzureAIAgentID);
        var agent = new AzureAIAgent(definition, _agentsClient, kernel.Plugins);

        var agentThread = new AzureAIAgentThread(agent.Client);
        if (request.FileUploads.Any())
        {
            var fileList = new List<AgentFile>();
            foreach (var inputFile in request.FileUploads)
            {
                var file = request.FileUploads.First();
                DataUriParser parser = new DataUriParser(file.DataUrl);
                var uploadFile = await _agentsClient.UploadFileAsync(new MemoryStream(parser.Data), AgentFilePurpose.Agents, file.FileName);
                fileList.Add(uploadFile);
            }

            var vectorStore = await _agentsClient.CreateVectorStoreAsync(fileList.Select(x => x.Id).ToList());
            FileSearchToolResource fileSearchToolResource = new FileSearchToolResource();
            fileSearchToolResource.VectorStoreIds.Add(vectorStore.Value.Id);
            agentThread = new AzureAIAgentThread(agent.Client, toolResources: new ToolResources() { FileSearch = fileSearchToolResource });
        }
   
        var message = new ChatMessageContent (AuthorRole.User, userMessage);
        await foreach (StreamingChatMessageContent contentChunk in agent.InvokeStreamingAsync(message, agentThread))
        {
            sb.Append(contentChunk.Content);
            yield return new ChatChunkResponse(contentChunk.Content);
            await Task.Yield();
        }
        sw.Stop();

        var contextData = new ResponseContext(profile.Name, null, Array.Empty<ThoughtRecord>(), request.ChatTurnId, request.ChatId, null);
        var result = new ApproachResponse(Answer: sb.ToString(),CitationBaseUrl: string.Empty, contextData);

        yield return new ChatChunkResponse(string.Empty, result);
    }
}
