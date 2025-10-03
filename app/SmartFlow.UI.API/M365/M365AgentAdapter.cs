// Copyright (c) Microsoft. All rights reserved.

using AgentActivity = Microsoft.Agents.Core.Models.Activity;
using Microsoft.Agents.Core.Models;
using MinimalApi.Agents;
using MinimalApi.Services.Profile;
using System.Text;

namespace MinimalApi.M365;

public class M365AgentAdapter
{
    private readonly IChatService _chatService;
    private readonly ProfileService _profileService;
    private readonly ILogger<M365AgentAdapter> _logger;

    public M365AgentAdapter(
        IChatService chatService,
        ProfileService profileService,
        ILogger<M365AgentAdapter> logger)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentActivity> ProcessActivityAsync(AgentActivity activity, CancellationToken cancellationToken = default)
    {
        try
        {
            if (activity.Type == ActivityTypes.ConversationUpdate && activity.MembersAdded?.Any() == true)
            {
                return HandleWelcome(activity);
            }
            else if (activity.Type == ActivityTypes.Message && !string.IsNullOrEmpty(activity.Text))
            {
                return await HandleMessageAsync(activity, cancellationToken);
            }

            return CreateResponse(activity, "Activity type not supported.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing activity");
            return CreateResponse(activity, "I apologize, but I encountered an error processing your request.");
        }
    }

    private AgentActivity HandleWelcome(AgentActivity activity)
    {
        var welcomeMessage = "Hello and Welcome! I'm your SmartFlow AI assistant. How can I help you today?";
        _logger.LogInformation("Sending welcome message");
        return CreateResponse(activity, welcomeMessage);
    }

    private async Task<AgentActivity> HandleMessageAsync(AgentActivity activity, CancellationToken cancellationToken)
    {
        try
        {
            var userMessage = activity.Text;
            _logger.LogInformation("Received message: {Message}", userMessage);

            var response = await ProcessMessageAsync(userMessage, activity, cancellationToken);
            return CreateResponse(activity, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message");
            return CreateResponse(activity, "I apologize, but I couldn't process your message.");
        }
    }

    private async Task<string> ProcessMessageAsync(string userMessage, AgentActivity activity, CancellationToken cancellationToken)
    {
        try
        {
            var profileInfo = await _profileService.GetProfileDataAsync();
            var userId = GetUserId(activity);
            var conversationId = GetConversationId(activity);
            
            var profile = profileInfo.Profiles.FirstOrDefault();
            if (profile == null)
            {
                return "No profiles configured. Please configure a profile in the system.";
            }

            var userInfo = new UserInformation(
                IsIdentityEnabled: false,
                UserName: activity.From?.Name ?? "M365User",
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

    private AgentActivity CreateResponse(AgentActivity incomingActivity, string text)
    {
        return new AgentActivity
        {
            Type = ActivityTypes.Message,
            Text = text,
            From = incomingActivity.Recipient,
            Recipient = incomingActivity.From,
            Conversation = incomingActivity.Conversation,
            ReplyToId = incomingActivity.Id
        };
    }

    private string GetConversationId(AgentActivity activity)
    {
        return activity.Conversation?.Id ?? Guid.NewGuid().ToString();
    }

    private string GetUserId(AgentActivity activity)
    {
        return activity.From?.Id ?? "anonymous";
    }
}
