// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.Agents.Persistent;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MinimalApi.Agents;

#pragma warning disable SKEXP0110
public class AzureAIAgentChatService : IChatService
{
    private readonly ILogger<AzureAIAgentChatService> _logger;
    private readonly IConfiguration _configuration;
    private readonly OpenAIClientFacade _openAIClientFacade;
    private readonly PersistentAgentsClient _agentsClient;
    public AzureAIAgentChatService(OpenAIClientFacade openAIClientFacade, AzureBlobStorageService blobStorageService, ILogger<AzureAIAgentChatService> logger, IConfiguration configuration)
    {
        _openAIClientFacade = openAIClientFacade;
        _logger = logger;
        _configuration = configuration;

        var azureAIFoundryProjectEndpoint = _configuration["AzureAIFoundryProjectEndpoint"];
        ArgumentNullException.ThrowIfNullOrEmpty(azureAIFoundryProjectEndpoint, "AzureAIFoundryProjectEndpoint");
        _agentsClient = AzureAIAgent.CreateAgentsClient(azureAIFoundryProjectEndpoint, new DefaultAzureCredential());
    }

    public async IAsyncEnumerable<ChatChunkResponse> ReplyAsync(UserInformation user, ProfileDefinition profile, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var sb = new StringBuilder();
        var userMessage = request.LastUserQuestion;

        var kernel = _openAIClientFacade.BuildKernel(string.Empty);
        var definition = await _agentsClient.Administration.GetAgentAsync(profile.AzureAIAgentID);
        var agent = new AzureAIAgent(definition, _agentsClient, kernel.Plugins);

        // Get or create a agent thread
        var agentThread = request.ThreadId != null
            ? await _agentsClient.Threads.GetThreadAsync(request.ThreadId)
            : await _agentsClient.Threads.CreateThreadAsync();

        if (request.FileUploads.Any())
        {
            var fileList = new List<PersistentAgentFileInfo>();
            foreach (var inputFile in request.FileUploads)
            {
                var file = request.FileUploads.First();
                DataUriParser parser = new DataUriParser(file.DataUrl);
                var uploadFile = await _agentsClient.Files.UploadFileAsync(new MemoryStream(parser.Data), PersistentAgentFilePurpose.Agents, file.FileName);
                fileList.Add(uploadFile);
            }

            // Check if the agent thread already has a vector store ID
            var vectorStoreId = agentThread.Value.ToolResources?.FileSearch?.VectorStoreIds?.FirstOrDefault();
            if (string.IsNullOrEmpty(vectorStoreId))
            {
                // Create a new vector store if it doesn't exist
                var vectorStore = await _agentsClient.VectorStores.CreateVectorStoreAsync(fileList.Select(x => x.Id).ToList());
                vectorStoreId = vectorStore.Value.Id;

                // Update the agent thread with the new vector store ID
                var fileSearchToolResource = new FileSearchToolResource();
                fileSearchToolResource.VectorStoreIds.Add(vectorStoreId);
                await _agentsClient.Threads.UpdateThreadAsync(agentThread.Value.Id, toolResources: new ToolResources() { FileSearch = fileSearchToolResource });
            }
            else
            {
                // Add the files to the existing vector store
                await _agentsClient.VectorStores.CreateVectorStoreFileAsync(vectorStoreId, fileList.FirstOrDefault().Id);
            }
        }

        var message = new ChatMessageContent(AuthorRole.User, userMessage);
        await foreach (StreamingChatMessageContent contentChunk in agent.InvokeStreamingAsync(message, new AzureAIAgentThread(_agentsClient,agentThread.Value.Id)))
        {
            sb.Append(contentChunk.Content);
            yield return new ChatChunkResponse(contentChunk.Content);
            await Task.Yield();
        }
        sw.Stop();

        var contextData = new ResponseContext(profile.Name, null, Array.Empty<ThoughtRecord>(), request.ChatTurnId, request.ChatId, null, null);
        var result = new ApproachResponse(Answer: sb.ToString(), CitationBaseUrl: string.Empty, contextData);

        yield return new ChatChunkResponse(string.Empty, result);
    }
}
