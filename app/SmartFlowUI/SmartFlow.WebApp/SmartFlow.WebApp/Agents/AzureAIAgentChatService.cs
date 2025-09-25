// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.AI.Agents.Persistent;
using Microsoft.SemanticKernel.Agents;
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
        var agent = new AzureAIAgent(definition, _agentsClient);

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

                //var files = _agentsClient.Files.GetFilesAsync();
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

                // Poll for vector store completion
                const int pollDelayMs = 1000;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var statusResult = await _agentsClient.VectorStores.GetVectorStoreAsync(vectorStoreId);
                    var status = statusResult.Value.Status;
                    if (status == VectorStoreStatus.Completed)
                        break;

                    await Task.Delay(pollDelayMs, cancellationToken);
                }

                // Update the agent thread with the new vector store ID
                var fileSearchToolResource = new FileSearchToolResource();
                fileSearchToolResource.VectorStoreIds.Add(vectorStoreId);
                var thread = await _agentsClient.Threads.UpdateThreadAsync(agentThread.Value.Id, toolResources: new ToolResources() { FileSearch = fileSearchToolResource });
                while (true)
                {
                    var threadStatus = await _agentsClient.Threads.GetThreadAsync(agentThread.Value.Id);
                    break;
                }
            }
            else
            {
                // Add the files to the existing vector store
                //var files = await _agentsClient.VectorStores.GetVectorStoreFilesAsync(vectorStoreId).ToListAsync();
                var fr = await _agentsClient.VectorStores.CreateVectorStoreFileAsync(vectorStoreId, fileList.FirstOrDefault().Id);
            }
        }

        var sources = new List<SupportingContentRecord>();
        var filesReferences = new List<string>();
        var message = new ChatMessageContent(AuthorRole.User, userMessage);
        await foreach (StreamingChatMessageContent contentChunk in agent.InvokeStreamingAsync(message, new AzureAIAgentThread(_agentsClient,agentThread.Value.Id)))
        {
            // Check if the contentChunk.Metadata contains code and ignore if that is the case
            if (contentChunk.Metadata != null && contentChunk.Metadata.TryGetValue("code", out object? codeValue) && codeValue is bool isCode && isCode)
            {
                // Skip streaming this chunk as it contains code
                continue;
            }

            sb.Append(contentChunk.Content);
            yield return new ChatChunkResponse(contentChunk.Content);
            await Task.Yield();

            foreach (StreamingAnnotationContent? annotation in contentChunk.Items.OfType<StreamingAnnotationContent>())
            {
                var tempContent = sb.ToString();
                var citationId = tempContent.Substring(annotation.StartIndex.Value, annotation.EndIndex.Value - annotation.StartIndex.Value);
                sources.Add(new SupportingContentRecord(annotation.Title,annotation.ReferenceId, "BING", citationId));
            }

            if (contentChunk.Items.OfType<StreamingFileReferenceContent>().Any())
            {
                var file = contentChunk.Items.OfType<StreamingFileReferenceContent>().FirstOrDefault();
                if (filesReferences.Contains(file.FileId))
                    continue;

                filesReferences.Add(file.FileId);

                // Generate enhanced image HTML with border and download button
                var imageId = Guid.NewGuid().ToString("N")[..8];
                var imageUrl = $"/api/images/{file.FileId}";
                var content = ImageHtmlGenerator.GenerateEnhancedImageHtml(imageUrl, imageId);

                sb.Append(content);
                yield return new ChatChunkResponse(content);

                await Task.Yield();
            }
        }
        sw.Stop();

        var contextData = new ResponseContext(profile.Name, sources.ToArray(), Array.Empty<ThoughtRecord>(), request.ChatTurnId, request.ChatId, agentThread.Value.Id, null);
        var result = new ApproachResponse(Answer: sb.ToString(), CitationBaseUrl: string.Empty, contextData);

        yield return new ChatChunkResponse(string.Empty, result);
    }
}
