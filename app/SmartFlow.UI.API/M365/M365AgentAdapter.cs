// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using MinimalApi.Agents;
using MinimalApi.Services.Profile;
using System.Text;

namespace MinimalApi.M365;

/// <summary>
/// Adapter class to integrate SmartFlow UI agents with Microsoft 365 Copilot chat.
/// This adapter bridges the existing chat services with the M365 Agent framework.
/// </summary>
public class M365AgentAdapter : AgentApplication
{
    private readonly IChatService _chatService;
    private readonly ProfileService _profileService;
    private readonly ILogger<M365AgentAdapter> _logger;

    public M365AgentAdapter(
        AgentApplicationOptions options, 
        IChatService chatService,
        ProfileService profileService,
        ILogger<M365AgentAdapter> logger) : base(options)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register event handlers
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    /// <summary>
    /// Handles welcome messages when new members are added to the conversation.
    /// </summary>
    private async Task WelcomeMessageAsync(
        ITurnContext turnContext, 
        ITurnState turnState, 
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    var welcomeMessage = "Hello and Welcome! I'm your SmartFlow AI assistant. How can I help you today?";
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text(welcomeMessage), 
                        cancellationToken);
                    
                    _logger.LogInformation("Sent welcome message to user: {UserId}", member.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome message");
            throw;
        }
    }

    /// <summary>
    /// Handles incoming messages and routes them through the SmartFlow chat service.
    /// </summary>
    private async Task OnMessageAsync(
        ITurnContext turnContext, 
        ITurnState turnState, 
        CancellationToken cancellationToken)
    {
        try
        {
            var userMessage = turnContext.Activity.Text;
            _logger.LogInformation("Received message from user: {Message}", userMessage);

            var response = await ProcessMessageAsync(userMessage, turnContext, turnState, cancellationToken);
            
            await turnContext.SendActivityAsync(
                response, 
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            await turnContext.SendActivityAsync(
                "I apologize, but I encountered an error processing your request. Please try again.",
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Processes the user's message through the SmartFlow chat service.
    /// </summary>
    private async Task<string> ProcessMessageAsync(
        string userMessage, 
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get profile data and user information
            var profileInfo = await _profileService.GetProfileDataAsync();
            var userId = GetUserId(turnContext);
            var conversationId = GetConversationId(turnContext);
            
            // Get the first available profile (you may want to make this configurable)
            var profile = profileInfo.Profiles.FirstOrDefault();
            if (profile == null)
            {
                return "No profiles configured. Please configure a profile in the system.";
            }

            // Create user information
            var userInfo = new UserInformation(
                IsIdentityEnabled: false,
                UserName: turnContext.Activity.From?.Name ?? "M365User",
                UserId: userId,
                SessionId: conversationId,
                Profiles: new[] { new ProfileSummary(
                    profile.Id,
                    profile.Name,
                    string.Empty,
                    (ProfileApproach)Enum.Parse(typeof(ProfileApproach), profile.Approach, true),
                    profile.SampleQuestions,
                    profile.UserPromptTemplates,
                    false,
                    profile.AllowFileUpload
                )},
                Groups: new[] { "M365Users" }
            );

            // Create chat request
            var chatRequest = new ChatRequest(
                ChatId: Guid.TryParse(conversationId, out var chatGuid) ? chatGuid : Guid.NewGuid(),
                ChatTurnId: Guid.NewGuid(),
                History: new[] { new ChatTurn(userMessage, string.Empty) },
                SelectedUserCollectionFiles: Array.Empty<string>(),
                FileUploads: Array.Empty<FileSummary>(),
                OptionFlags: new Dictionary<string, string> { { "profile", profile.Id } },
                UserSelectionModel: null,
                ThreadId: conversationId
            );

            // Call chat service and collect response
            var responseBuilder = new StringBuilder();
            await foreach (var chunk in _chatService.ReplyAsync(userInfo, profile, chatRequest, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Text))
                {
                    responseBuilder.Append(chunk.Text);
                }
                
                if (chunk.FinalResult != null)
                {
                    _logger.LogInformation("Chat completed. Response length: {Length}", responseBuilder.Length);
                }
            }

            var response = responseBuilder.ToString();
            return string.IsNullOrEmpty(response) ? "I apologize, but I couldn't generate a response." : response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessMessageAsync");
            throw;
        }
    }

    /// <summary>
    /// Retrieves or creates conversation state for the current turn.
    /// </summary>
    private string GetConversationId(ITurnContext turnContext)
    {
        return turnContext.Activity.Conversation?.Id ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Retrieves the user identifier from the current turn context.
    /// </summary>
    private string GetUserId(ITurnContext turnContext)
    {
        return turnContext.Activity.From?.Id ?? "anonymous";
    }
}
