using Microsoft.SemanticKernel.ChatCompletion;
using TiktokenSharp;

namespace MinimalApi.Extensions;

public static class SKExtensions
{
    public static KernelArguments AddUserParameters(this KernelArguments arguments, ChatRequest request, ProfileDefinition profile, UserInformation user)
    {
        arguments[ContextVariableOptions.Profile] = profile;
        arguments[ContextVariableOptions.UserId] = user.UserId;
        arguments[ContextVariableOptions.SessionId] = user.SessionId;

        if (request.SelectedUserCollectionFiles != null && request.SelectedUserCollectionFiles.Any())
        {
            arguments[ContextVariableOptions.SelectedDocuments] = request.SelectedUserCollectionFiles;
        }

        if (request.History.LastOrDefault()?.User is { } userQuestion)
        {
            arguments[ContextVariableOptions.Question] = $"{userQuestion}";
        }
        else
        {
            throw new InvalidOperationException("User question is null");
        }

        if (request.UserSelectionModel != null && request.UserSelectionModel.Options.Any())
        {
            foreach (var item in request.UserSelectionModel.Options)
            {
                arguments[item.Name] = item.SelectedValue;
            }
        }


        arguments[ContextVariableOptions.ChatTurns] = request.History;
        return arguments;
    }


    public static ChatHistory AddChatHistory(this ChatHistory chatHistory, ChatTurn[] history)
    {
        foreach (var chatTurn in history.SkipLast(1))
        {
            chatHistory.AddUserMessage(chatTurn.User);
            if (chatTurn.Assistant != null)
            {
                chatHistory.AddAssistantMessage(chatTurn.Assistant);
            }
        }

        return chatHistory;
    }


    public static bool IsChatProfile(this Dictionary<string, string> options, List<ProfileDefinition> profiles)
    {
        var profile = options.GetChatProfile(profiles);
        var selected = profiles.FirstOrDefault(x => x.Name == profile.Name);
        return selected?.Approach.ToUpper() == "CHAT";
    }

    public static bool IsEndpointAssistantProfile(this Dictionary<string, string> options, List<ProfileDefinition> profiles)
    {
        var profile = options.GetChatProfile(profiles);
        var selected = profiles.FirstOrDefault(x => x.Name == profile.Name);
        return selected?.Approach.ToUpper() == "ENDPOINTASSISTANT";
    }
    public static bool IsEndpointAssistantV2Profile(this Dictionary<string, string> options, List<ProfileDefinition> profiles)
    {
        var profile = options.GetChatProfile(profiles);
        var selected = profiles.FirstOrDefault(x => x.Name == profile.Name);
        return selected?.Approach.ToUpper() == "ENDPOINTASSISTANTV2";
    }
    public static bool IsEndpointAssistantTaskProfile(this Dictionary<string, string> options, List<ProfileDefinition> profiles)
    {
        var profile = options.GetChatProfile(profiles);
        var selected = profiles.FirstOrDefault(x => x.Name == profile.Name);
        return selected?.Approach.ToUpper() == "ENDPOINTASSISTANTTASK";
    }

    public static bool IsAzureAIAgentChatProfile(this Dictionary<string, string> options, List<ProfileDefinition> profiles)
    {
        var profile = options.GetChatProfile(profiles);
        var selected = profiles.FirstOrDefault(x => x.Name == profile.Name);
        return selected?.Approach.ToUpper() == "AZUREAIAGENTCHATPROFILE";
    }
    public static bool IsImangeChatProfile(this Dictionary<string, string> options, List<ProfileDefinition> profiles)
    {
        var profile = options.GetChatProfile(profiles);
        var selected = profiles.FirstOrDefault(x => x.Name == profile.Name);
        return selected?.Approach.ToUpper() == "IMAGECHAT";
    }
    public static ProfileDefinition GetChatProfile(this Dictionary<string, string> options, List<ProfileDefinition> profiles)
    {
        var defaultProfile = profiles.First();
        var value = options.GetValueOrDefault("PROFILE", defaultProfile.Name);
        return profiles.FirstOrDefault(x => x.Name == value) ?? defaultProfile;
    }


    public static ApproachResponse BuildStreamingResoponse(this KernelArguments context, Kernel kernel, ProfileDefinition profile, ChatRequest request, ChatHistory chatHistory, string answer, IConfiguration configuration, string modelDeploymentName, long workflowDurationMilliseconds)
    {
        var requestTokenCount = chatHistory.GetTokenCount();

        var dataSources = new List<SupportingContentRecord>();
        var functionCallResults = kernel.GetFunctionCallResults();
        foreach (var result in functionCallResults)
        {
            if (result.Sources != null && result.Sources.Any())
            {
                dataSources.AddRange(result.Sources);
            }
        }

        var completionTokens = GetTokenCount(answer);
        var totalTokens = completionTokens + requestTokenCount;
        var chatDiagnostics = new CompletionsDiagnostics(completionTokens, requestTokenCount, totalTokens, 0);
        var diagnostics = new Diagnostics(chatDiagnostics, modelDeploymentName, workflowDurationMilliseconds);

        var thoughts = kernel.GetThoughtProcess(chatHistory.FirstOrDefault(x => x.Role == AuthorRole.System).Content, answer);
        var contextData = new ResponseContext(
            profile.Name, 
            dataSources.Distinct().ToArray(), // Remove duplicates
            thoughts.Select(x => new ThoughtRecord(x.Name, x.Result)).ToArray(), 
            request.ChatTurnId, 
            request.ChatId,
            null,
            diagnostics);

        return new ApproachResponse(
            Answer: NormalizeResponseText(answer),
            CitationBaseUrl: profile.Id,
            contextData);
    }

    public static ApproachResponse BuildChatSimpleResponse(this KernelArguments context, ProfileDefinition profile, ChatRequest request, int requestTokenCount, string answer, IConfiguration configuration, string modelDeploymentName, long workflowDurationMilliseconds)
    {
        var completionTokens = GetTokenCount(answer);
        var totalTokens = completionTokens + requestTokenCount;
        var chatDiagnostics = new CompletionsDiagnostics(completionTokens, requestTokenCount, totalTokens, 0);
        var diagnostics = new Diagnostics(chatDiagnostics, modelDeploymentName, workflowDurationMilliseconds);

        var contextData = new ResponseContext(profile.Name, null, Array.Empty<ThoughtRecord>(), request.ChatTurnId, request.ChatId, null, diagnostics);

        return new ApproachResponse(
            Answer: NormalizeResponseText(answer),
            CitationBaseUrl: string.Empty,
            contextData);
    }

    private static string NormalizeResponseText(string text)
    {
        text = text.StartsWith("null,") ? text[5..] : text;
        text = text.Replace("\r", "\n")
            .Replace("\\n\\r", "\n")
            .Replace("\\n", "\n");

        return text;
    }
    public static int GetTokenCount(this ChatHistory chatHistory)
    {
        string requestContent = string.Join("", chatHistory.Select(x => x.Content));
        var tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");
        return tikToken.Encode(requestContent).Count;
    }

    public static int GetTokenCount(string text)
    {
        var tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");
        return tikToken.Encode(text).Count;
    }

    public static void AddFunctionCallResult(this Kernel kernel, string name, string result, List<KnowledgeSource> sources = null)
    {
        var diagnosticsBuilder = GetRequestDiagnosticsBuilder(kernel);
        if (sources != null && sources.Any())
        {
            var supportingContent = sources.Select(x => new SupportingContentRecord(x.FilePath, x.Content, "FUNCTION", string.Empty)).ToList();
            diagnosticsBuilder.AddFunctionCallResult(name, result, supportingContent);
        }
        else
        {
            diagnosticsBuilder.AddFunctionCallResult(name, result);
        }
    }

    public static RequestDiagnosticsBuilder GetRequestDiagnosticsBuilder(this Kernel kernel)
    {
        if (!kernel.Data.ContainsKey("DiagnosticsBuilder"))
        {
            var diagnosticsBuilder = new RequestDiagnosticsBuilder();
            kernel.Data.Add("DiagnosticsBuilder", diagnosticsBuilder);
            return diagnosticsBuilder;
        }

        return kernel.Data["DiagnosticsBuilder"] as RequestDiagnosticsBuilder;
    }
    public static IEnumerable<ExecutionStepResult> GetFunctionCallResults(this Kernel kernel)
    {
        if (kernel.Data.ContainsKey("DiagnosticsBuilder"))
        {
            var diagnosticsBuilder = kernel.Data["DiagnosticsBuilder"] as RequestDiagnosticsBuilder;
            foreach (var item in diagnosticsBuilder.FunctionCallResults)
            {
                yield return item;
            }
        }
    }

    public static IEnumerable<ExecutionStepResult> GetThoughtProcess(this Kernel kernel, string systemPrompt, string answer)
    {
        var functionCallResults = kernel.GetFunctionCallResults();
        foreach (var item in functionCallResults)
        {
            yield return item;
        }
        yield return new ExecutionStepResult("chat_completion", $"{systemPrompt} \n {answer}");
    }
}

public static class VectorSearchExtensions
{
    /// <summary>
    /// Creates VectorSearchSettings from profile RAGSettings and adds it to the kernel
    /// </summary>
    /// <param name="kernel">The kernel to add settings to</param>
    /// <param name="profile">Profile containing RAG settings</param>
    /// <returns>The provided kernel for chaining</returns>
    public static Kernel AddVectorSearchSettings(this Kernel kernel, ProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile?.RAGSettings, "profile.RAGSettings");
        
        kernel.Data["VectorSearchSettings"] = new VectorSearchSettings(
            profile.RAGSettings.DocumentRetrievalIndexName,
            profile.RAGSettings.DocumentRetrievalDocumentCount,
            profile.RAGSettings.DocumentRetrievalSchema,
            profile.RAGSettings.DocumentRetrievalEmbeddingsDeployment,
            profile.RAGSettings.DocumentRetrievalMaxSourceTokens,
            profile.RAGSettings.KNearestNeighborsCount,
            profile.RAGSettings.Exhaustive,
            profile.RAGSettings.UseSemanticRanker,
            profile.RAGSettings.SemanticConfigurationName,
            profile.RAGSettings.StorageContianer,
            profile.RAGSettings.CitationUseSourcePage);
            
        return kernel;
    }
}

