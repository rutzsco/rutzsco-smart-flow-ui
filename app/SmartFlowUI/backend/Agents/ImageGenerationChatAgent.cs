// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextToImage;
using MinimalApi.Services.Profile.Prompts;
using System.Runtime.CompilerServices; // Required for EnumeratorCancellation
using System.Text.RegularExpressions; // Required for Regex

namespace MinimalApi.Agents;

#pragma warning disable SKEXP0001
internal sealed class ImageGenerationChatAgent : IChatService
{
    private readonly ILogger<RAGChatService> _logger;
    private readonly IConfiguration _configuration;
    private readonly OpenAIClientFacade _openAIClientFacade;
    private readonly TextToImageService _textToImageService;

    public ImageGenerationChatAgent(OpenAIClientFacade openAIClientFacade,
                                    TextToImageService textToImageService,
                                    ILogger<RAGChatService> logger,
                                    IConfiguration configuration)
    {
        _openAIClientFacade = openAIClientFacade;
        _textToImageService = textToImageService;
        _logger = logger;
        _configuration = configuration;
    }

    public async IAsyncEnumerable<ChatChunkResponse> ReplyAsync(UserInformation user, ProfileDefinition profile, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var userMessage = request.LastUserQuestion;
        string? finalImageUrl = null;

        string? previousImageUrl = TryExtractPreviousImageUrl(request.History);

        if (previousImageUrl != null)
        {
            finalImageUrl = await AttemptEditImageAsync(previousImageUrl, userMessage, cancellationToken);
        }

        if (string.IsNullOrEmpty(finalImageUrl))
        {
            finalImageUrl = await GenerateNewImageAsync(userMessage, cancellationToken);
        }

        if (string.IsNullOrEmpty(finalImageUrl))
        {
            _logger.LogWarning("No images could be generated or edited for the user message: {UserMessage}", userMessage);
            yield return new ChatChunkResponse("No images could be generated or edited.", null);
            yield break;
        }

        var markdownString = $"![Generated Image]({finalImageUrl})";
        var result = new ApproachResponse(markdownString, null, null);
        yield return new ChatChunkResponse(string.Empty, result);
    }

    private string? TryExtractPreviousImageUrl(ChatTurn[]? history)
    {
        if (history == null || !history.Any())
        {
            return null;
        }

        var lastTurnWithAssistantMessage = history
            .Where(turn => !string.IsNullOrEmpty(turn.Assistant))
            .LastOrDefault();

        if (lastTurnWithAssistantMessage != null)
        {
            var match = Regex.Match(lastTurnWithAssistantMessage.Assistant!, @"!\[.*?\]\((.*?)\)");
            if (match.Success && match.Groups.Count > 1)
            {
                var imageUrl = match.Groups[1].Value;
                _logger.LogInformation("Found previous image URL: {PreviousImageUrl}", imageUrl);
                return imageUrl;
            }
        }
        return null;
    }

    private async Task<string?> AttemptEditImageAsync(string imageUrl, string prompt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to edit previous image using URL: {ImageUrl} with prompt: {Prompt}", imageUrl, prompt);
        try
        {
            // Pass cancellationToken if EditImageFromDataUrlAsync supports it, otherwise remove.
            // Assuming EditImageFromDataUrlAsync does not take a CancellationToken based on previous context.
            string? editedImageUrl = await _textToImageService.EditImageFromDataUrlAsync(imageUrl, prompt); // Default size, n, quality
            if (!string.IsNullOrEmpty(editedImageUrl))
            {
                _logger.LogInformation("Successfully edited image. New URL: {EditedImageUrl}", editedImageUrl);
                return editedImageUrl;
            }
            else
            {
                _logger.LogWarning("Editing image with URL {ImageUrl} did not return a new URL. Falling back to new image generation.", imageUrl);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing image with URL {ImageUrl}. Falling back to new image generation.", imageUrl);
            return null;
        }
    }

    private async Task<string?> GenerateNewImageAsync(string prompt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating new image for prompt: {Prompt}", prompt);
        try
        {
            var kernel = _openAIClientFacade.BuildKernel("ImageGen");
            var imageGenerationKernelService = kernel.GetRequiredService<ITextToImageService>();
            
            var generatedImages = await imageGenerationKernelService.GetImageContentsAsync(
                new TextContent(prompt),
                new OpenAITextToImageExecutionSettings { Size = (Width: 1024, Height: 1024) }, 
                cancellationToken: cancellationToken);

            var firstImageUrl = generatedImages.FirstOrDefault()?.Uri?.ToString();
            if (!string.IsNullOrEmpty(firstImageUrl))
            {
                _logger.LogInformation("Successfully generated new image. URL: {ImageUrl}", firstImageUrl);
                return firstImageUrl;
            }
            _logger.LogWarning("Image generation did not return a URL for prompt: {Prompt}", prompt);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating new image for prompt: {Prompt}", prompt);
            return null;
        }
    }
}

#pragma warning restore SKEXP0001
