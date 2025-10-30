// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MinimalApi.Services.Profile.Prompts;

namespace MinimalApi.Agents;

internal sealed class RAGChatService : IChatService
{
    private readonly ILogger<RAGChatService> _logger;
    private readonly IConfiguration _configuration;
    private readonly OpenAIClientFacade _openAIClientFacade;

    public RAGChatService(OpenAIClientFacade openAIClientFacade,
                                                ILogger<RAGChatService> logger,
                                                IConfiguration configuration)
    {
        _openAIClientFacade = openAIClientFacade;
        _logger = logger;
        _configuration = configuration;
    }

    public async IAsyncEnumerable<ChatChunkResponse> ReplyAsync(UserInformation user, ProfileDefinition profile, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile.RAGSettings, "profile.RAGSettings");
        var sw = Stopwatch.StartNew();

        // Kernel setup
        var kernel = _openAIClientFacade.BuildKernel("RAG");
        kernel.AddVectorSearchSettings(profile);

        var context = new KernelArguments().AddUserParameters(request, profile, user);

        // Chat Step
        var chatGpt = kernel.Services.GetService<IChatCompletionService>();
        var systemMessagePrompt = ResolveSystemMessage(profile);
        context[ContextVariableOptions.SystemMessagePrompt] = systemMessagePrompt;
        var chatHistory = new ChatHistory(systemMessagePrompt).AddChatHistory(request.History);


        var userMessage = await ResolveUserMessageAsync(profile, kernel, context);
        context[ContextVariableOptions.UserMessage] = userMessage;
        if (request.FileUploads.Any())
        {
            ChatMessageContentItemCollection chatMessageContentItemCollection = new ChatMessageContentItemCollection();
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
        await foreach (StreamingChatMessageContent chatUpdate in chatGpt.GetStreamingChatMessageContentsAsync(chatHistory, DefaultSettings.AIChatWithToolsRequestSettings, kernel, cancellationToken))
            if (chatUpdate.Content != null)
            {
                sb.Append(chatUpdate.Content);
                yield return new ChatChunkResponse( chatUpdate.Content);
                await Task.Yield();
            }
        sw.Stop();

        var result = context.BuildStreamingResoponse(kernel, profile, request, chatHistory, sb.ToString(), _configuration, _openAIClientFacade.GetKernelDeploymentName(), sw.ElapsedMilliseconds);

        _logger.LogInformation($"Chat Complete - Profile: {result.Context.Profile}, ChatId: {result.Context.ChatId}, ChatMessageId: {result.Context.MessageId}, ModelDeploymentName: {result.Context.Diagnostics.ModelDeploymentName}, TotalTokens: {result.Context.Diagnostics.AnswerDiagnostics.TotalTokens}");

        yield return new ChatChunkResponse(string.Empty, result);
    }


    private string ResolveSystemMessage(ProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile.RAGSettings, "profile.RAGSettings");

        var systemMessagePrompt = string.Empty;
        if (!string.IsNullOrEmpty(profile.RAGSettings.ChatSystemMessageFile))
            systemMessagePrompt = PromptService.GetPromptByName(profile.RAGSettings.ChatSystemMessageFile);

        if (!string.IsNullOrEmpty(profile.RAGSettings.ChatSystemMessage))
        {
            var bytes = Convert.FromBase64String(profile.RAGSettings.ChatSystemMessage);
            systemMessagePrompt = Encoding.UTF8.GetString(bytes);
        }
        return systemMessagePrompt;
    }

    private async Task<string> ResolveUserMessageAsync(ProfileDefinition profile, Kernel kernel, KernelArguments context)
    {
        ArgumentNullException.ThrowIfNull(profile.RAGSettings, "profile.RAGSettings");

        var userMessage = string.Empty;
        if (!string.IsNullOrEmpty(profile.RAGSettings.ChatUserMessage))
        {
            var bytes = Convert.FromBase64String(profile.RAGSettings.ChatUserMessage);
            userMessage = Encoding.UTF8.GetString(bytes);
        }
        else
            userMessage = await PromptService.RenderPromptAsync(kernel, PromptService.GetPromptByName(PromptService.ChatUserPrompt), context);

        return userMessage;
    }
}
