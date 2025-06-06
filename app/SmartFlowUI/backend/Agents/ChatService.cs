﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.ChatCompletion;
using MinimalApi.Services.Profile.Prompts;

namespace MinimalApi.Agents;

internal sealed class ChatService : IChatService
{
    private readonly ILogger<RAGChatService> _logger;
    private readonly IConfiguration _configuration;
    private readonly OpenAIClientFacade _openAIClientFacade;
    private readonly AzureBlobStorageService _blobStorageService;

    public ChatService(OpenAIClientFacade openAIClientFacade, AzureBlobStorageService blobStorageService, ILogger<RAGChatService> logger, IConfiguration configuration)
    {
        _openAIClientFacade = openAIClientFacade;
        _blobStorageService = blobStorageService;
        _logger = logger;
        _configuration = configuration;
    }


    public async IAsyncEnumerable<ChatChunkResponse> ReplyAsync(UserInformation user, ProfileDefinition profile, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {

        var sw = Stopwatch.StartNew();

        var kernel = _openAIClientFacade.BuildKernel(string.Empty);
        var context = new KernelArguments().AddUserParameters(request, profile, user);

        // Chat Step
        var chatGpt = kernel.Services.GetService<IChatCompletionService>();
        var systemMessagePrompt = string.Empty;
        if (!string.IsNullOrEmpty(profile.ChatSystemMessageFile))
        {
            systemMessagePrompt = PromptService.GetPromptByName(profile.ChatSystemMessageFile);
            context["SystemMessagePrompt"] = systemMessagePrompt;
        }

        if (!string.IsNullOrEmpty(profile.ChatSystemMessage))
        {
            var bytes = Convert.FromBase64String(profile.ChatSystemMessage);
            systemMessagePrompt = Encoding.UTF8.GetString(bytes);
            context[ContextVariableOptions.SystemMessagePrompt] = systemMessagePrompt;
        }

        var chatHistory = new ChatHistory(systemMessagePrompt).AddChatHistory(request.History);
        var userMessage = await PromptService.RenderPromptAsync(kernel, PromptService.GetPromptByName(PromptService.ChatSimpleUserPrompt), context);
        context["UserMessage"] = userMessage;



        if (request.FileUploads.Any())
        {
            var chatMessageContentItemCollection = new ChatMessageContentItemCollection();
            chatMessageContentItemCollection.Add(new TextContent(userMessage));

            foreach (var file in request.FileUploads)
            {
                DataUriParser parser = new DataUriParser(file.DataUrl);
                if (parser.MediaType == "image/jpeg" || parser.MediaType == "image/png")
                    chatMessageContentItemCollection.Add(new ImageContent(parser.Data, parser.MediaType));
                else if (parser.MediaType == "application/pdf")
                {
                    string pdfData = PDFTextExtractor.ExtractTextFromPdf(parser.Data);
                    chatMessageContentItemCollection.Add(new TextContent(pdfData));
                }
                else
                {
                    string csvData = Encoding.UTF8.GetString(parser.Data);
                    chatMessageContentItemCollection.Add(new TextContent(csvData));

                }
            }

            chatHistory.AddUserMessage(chatMessageContentItemCollection);
        }
        else
            chatHistory.AddUserMessage(userMessage);

        var sb = new StringBuilder();
        await foreach (StreamingChatMessageContent chatUpdate in chatGpt.GetStreamingChatMessageContentsAsync(chatHistory, DefaultSettings.AIChatRequestSettings))
            if (chatUpdate.Content != null)
            {
                sb.Append(chatUpdate.Content);
                yield return new ChatChunkResponse(chatUpdate.Content);
                await Task.Yield();
            }
        sw.Stop();


        var requestTokenCount = chatHistory.GetTokenCount();
        var result = context.BuildChatSimpleResponse(profile, request, requestTokenCount, sb.ToString(), _configuration, _openAIClientFacade.GetKernelDeploymentName(), sw.ElapsedMilliseconds);
        yield return new ChatChunkResponse(string.Empty, result);
    }
}
