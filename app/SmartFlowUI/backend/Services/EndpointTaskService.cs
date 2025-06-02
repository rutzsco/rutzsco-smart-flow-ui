// Copyright (c) Microsoft. All rights reserved.

using MinimalApi.Agents;

namespace MinimalApi.Services;

internal sealed class EndpointTaskService : IChatService
{
    private readonly ILogger<EndpointChatService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public EndpointTaskService(ILogger<EndpointChatService> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async IAsyncEnumerable<ChatChunkResponse> ReplyAsync(UserInformation user, ProfileDefinition profile, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var url = _configuration[profile.AssistantEndpointSettings.APIEndpointSetting];
        var payload = string.Empty;

        var apiRequest = new HttpRequestMessage(HttpMethod.Post, url);
        apiRequest.Headers.Add("X-Api-Key", _configuration[profile.AssistantEndpointSettings.APIEndpointKeySetting]);
        apiRequest.Content = BuildTaskRequest(request);
        if (apiRequest.Content == null)
        {
            var msg = $"System Error: Unable to create request for {profile.Name}";
            payload = BuildErrorTaskResponsePayload(msg, url, profile.Name, "BuildRequest");
        }
        try
        {
            if (payload == string.Empty)
            {
                var response = await _httpClient.SendAsync(apiRequest);
                response.EnsureSuccessStatusCode();
                payload = await response.Content.ReadAsStringAsync();
            }
        }
        catch (HttpRequestException ex)
        {
            var msg =
                ex.StatusCode.Value == System.Net.HttpStatusCode.NotFound ? $"System Error: API for {profile.Name} not found!" :
                ex.StatusCode.Value == System.Net.HttpStatusCode.TooManyRequests ? "System Error: Rate Limit exceeded!" :
                "System Error: Unable to get a response from the server.";
            payload = BuildErrorTaskResponsePayload(msg, url, profile.Name, "SendAsync");
        }
        catch (JsonException)
        {
            var msg = $"Error: Failed to parse the server response - JSON Exception for {profile.Name}";
            payload = BuildErrorTaskResponsePayload(msg, url, profile.Name, "SendAsync");
        }

        var taskResponse = System.Text.Json.JsonSerializer.Deserialize<TaskResponse>(payload);
        var thoughts = new List<ThoughtRecord>();
        foreach (var thought in taskResponse.thoughtProcess)
        {
            thoughts.Add(new ThoughtRecord(FormatLogStep(thought), thought.content));
        }
        yield return new ChatChunkResponse("", new ApproachResponse(taskResponse.answer, null, new ResponseContext(profile.Name, null, thoughts.ToArray(), request.ChatTurnId, request.ChatId, null, null)));
    }
    private string BuildErrorTaskResponsePayload(string msg, string url, string profileName, string actionName)
    {
        var msgEx = $"{msg} Calling URL: {url}";
        Console.WriteLine(msgEx);
        var logEntries = new List<WorkflowLogEntry> { new(profileName, actionName, msgEx, null) };
        var errorTaskResponse = new TaskResponse(msg, logEntries.AsEnumerable(), msgEx);
        var payload = System.Text.Json.JsonSerializer.Serialize(errorTaskResponse);
        return payload;
    }
    private StringContent BuildChatRequest(ChatRequest request)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(request.History);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        return content;
    }

    public StringContent BuildTaskRequest(ChatRequest request)
    {
        try
        {
            var file = request.FileUploads.FirstOrDefault();
            var requestModel = new
            {
                task = request.ChatTurnId,
                requestMessage = "",
                files = new[]
                {
                new
                {
                    name = "Label",
                    dataUrl = file.DataUrl
                }
            }
            };
            var payload = System.Text.Json.JsonSerializer.Serialize(requestModel);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            return content;
        }
        catch
        {
            return null;
        }
    }

    private string FormatLogStep(WorkflowLogEntry logEntry)
    {
        if (logEntry.diagnostics == null)
            return $"{logEntry.agentName}-{logEntry.step}";

        return $"{logEntry.agentName}-{logEntry.step} ({logEntry.diagnostics.elapsedMilliseconds} milliseconds)";
    }
}

public record TaskResponse(string answer, IEnumerable<WorkflowLogEntry> thoughtProcess, string? error = null);

public record WorkflowLogEntry(string agentName, string step, string? content, WorkflowStepDiagnostics? diagnostics);

public record WorkflowStepDiagnostics(long elapsedMilliseconds);


