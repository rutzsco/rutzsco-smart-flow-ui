// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextToImage;
using MinimalApi.Services.Profile.Prompts;

namespace MinimalApi.Agents;

#pragma warning disable SKEXP0001
internal sealed class ImageGenerationChatAgent : IChatService
{
    private readonly ILogger<RAGChatService> _logger;
    private readonly IConfiguration _configuration;
    private readonly OpenAIClientFacade _openAIClientFacade;

    public ImageGenerationChatAgent(OpenAIClientFacade openAIClientFacade,
                                    ILogger<RAGChatService> logger,
                                    IConfiguration configuration)
    {
        _openAIClientFacade = openAIClientFacade;
        _logger = logger;
        _configuration = configuration;
    }

    public async IAsyncEnumerable<ChatChunkResponse> ReplyAsync(UserInformation user, ProfileDefinition profile, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Kernel setup
        var kernel = _openAIClientFacade.BuildKernel("ImageGen");
        var service = kernel.GetRequiredService<ITextToImageService>();

        var userMessage = request.LastUserQuestion;
        var generatedImages = await service.GetImageContentsAsync(new TextContent(userMessage),
            new OpenAITextToImageExecutionSettings { Size = (Width: 1024, Height: 1024) });

        // Extract the first image URL
        var firstImageUrl = generatedImages.FirstOrDefault()?.Uri;
        if (firstImageUrl == null)
        {
            _logger.LogWarning("No images were generated for the user message: {UserMessage}", userMessage);
            yield return new ChatChunkResponse("No images could be generated.", null);
            yield break;
        }

        // Create a markdown string to render the image
        var markdownString = $"![Generated Image]({firstImageUrl})";

        var result = new ApproachResponse(markdownString, null, null);
        yield return new ChatChunkResponse(string.Empty, result);
    }

}

#pragma warning restore SKEXP0001
