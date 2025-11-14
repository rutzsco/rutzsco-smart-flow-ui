// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.UI.Client.Services;

public sealed class ApiClient(HttpClient httpClient)
{
    private static UserInformation? _UserInformation = null;
    public async Task IngestionTriggerAsync(string sourceContainer, string indexName)
    {
        var request = new IngestionRequest(sourceContainer, $"{sourceContainer}-extract", indexName);
        var json = System.Text.Json.JsonSerializer.Serialize(request, SerializerOptions.Default);
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("api/ingestion/trigger", body);
    }
    
    // Collection Management APIs
    public async Task<List<CollectionInfo>> GetCollectionsAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("api/collections");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<CollectionInfo>>() ?? new List<CollectionInfo>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching collections: {ex.Message}");
            return new List<CollectionInfo>();
        }
    }

    public async Task<bool> CreateCollectionAsync(string containerName, string? description = null, string? type = null)
    {
        try
        {
            var request = new CreateCollectionRequest
            {
                Name = containerName,
                Description = description,
                Type = type
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(request, SerializerOptions.Default);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("api/collections", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating collection: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteCollectionAsync(string containerName)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"api/collections/{containerName}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting collection: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateCollectionMetadataAsync(string containerName, string? description = null, string? type = null)
    {
        try
        {
            var request = new CreateCollectionRequest
            {
                Name = containerName,
                Description = description,
                Type = type
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(request, SerializerOptions.Default);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync($"api/collections/{containerName}/metadata", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating collection metadata: {ex.Message}");
            return false;
        }
    }

    public async Task<CollectionInfo?> GetCollectionMetadataAsync(string containerName)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/collections/{containerName}/metadata");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CollectionInfo>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching collection metadata: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ContainerFileInfo>> GetCollectionFilesAsync(string containerName)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/collections/{containerName}/files");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ContainerFileInfo>>() ?? new List<ContainerFileInfo>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching collection files: {ex.Message}");
            return new List<ContainerFileInfo>();
        }
    }

    public async Task<bool> DeleteFileFromCollectionAsync(string containerName, string fileName)
    {
        try
        {
            var encodedFileName = Uri.EscapeDataString(fileName);
            var response = await httpClient.DeleteAsync($"api/collections/{containerName}/files/{encodedFileName}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting file from collection: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ProcessDocumentLayoutAsync(string containerName, string fileName)
    {
        try
        {
            var response = await httpClient.PostAsync($"api/collections/{containerName}/process/{fileName}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error processing document layout: {ex.Message}");
            return false;
        }
    }

    public async Task<string?> GetFileUrlAsync(string containerName, string fileName)
    {
        try
        {
            var encodedFileName = Uri.EscapeDataString(fileName);
            return $"api/collections/{containerName}/download/{encodedFileName}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error generating file URL: {ex.Message}");
            return null;
        }
    }

    public async Task<UploadDocumentsResponse> UploadFilesToCollectionAsync(IReadOnlyList<IBrowserFile> files, long maxAllowedSize, string containerName, IDictionary<string, string>? metadata = null)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            foreach (var file in files)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize));
#pragma warning restore CA2000 // Dispose objects before losing scope
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                content.Add(fileContent, file.Name, file.Name);
            }

            var tokenResponse = await httpClient.GetAsync("api/token/csrf");
            tokenResponse.EnsureSuccessStatusCode();
            var token = await tokenResponse.Content.ReadAsStringAsync();
            token = token.Trim('"');

            content.Headers.Add("X-CSRF-TOKEN-FORM", token);
            content.Headers.Add("X-CSRF-TOKEN-HEADER", token);

            if (metadata != null)
            {
                string serializedHeaders = System.Text.Json.JsonSerializer.Serialize(metadata);
                content.Headers.Add("X-FILE-METADATA", serializedHeaders);
            }

            var response = await httpClient.PostAsync($"api/collections/{containerName}/upload", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<UploadDocumentsResponse>();
            return result ?? UploadDocumentsResponse.FromError("Unable to upload files, unknown error.");
        }
        catch (Exception ex)
        {
            return UploadDocumentsResponse.FromError(ex.ToString());
        }
    }

    // Project Management APIs
    public async Task<List<CollectionInfo>> GetProjectsAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("api/projects");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<CollectionInfo>>() ?? new List<CollectionInfo>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching projects: {ex.Message}");
            return new List<CollectionInfo>();
        }
    }

    public async Task<bool> CreateProjectAsync(string projectName, string? description = null, string? type = null)
    {
        try
        {
            var request = new CreateCollectionRequest
            {
                Name = projectName,
                Description = description,
                Type = type
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(request, SerializerOptions.Default);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("api/projects", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating project: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteProjectAsync(string projectName)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"api/projects/{projectName}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting project: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateProjectMetadataAsync(string projectName, string? description = null, string? type = null)
    {
        try
        {
            var request = new CreateCollectionRequest
            {
                Name = projectName,
                Description = description,
                Type = type
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(request, SerializerOptions.Default);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync($"api/projects/{projectName}/metadata", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating project metadata: {ex.Message}");
            return false;
        }
    }

    public async Task<CollectionInfo?> GetProjectMetadataAsync(string projectName)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/projects/{projectName}/metadata");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CollectionInfo>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching project metadata: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ContainerFileInfo>> GetProjectFilesAsync(string projectName)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/projects/{projectName}/files");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ContainerFileInfo>>() ?? new List<ContainerFileInfo>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching project files: {ex.Message}");
            return new List<ContainerFileInfo>();
        }
    }

    public async Task<bool> DeleteFileFromProjectAsync(string projectName, string fileName)
    {
        try
        {
            var encodedFileName = Uri.EscapeDataString(fileName);
            var response = await httpClient.DeleteAsync($"api/projects/{projectName}/files/{encodedFileName}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting file from project: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ProcessProjectDocumentLayoutAsync(string projectName, string fileName)
    {
        try
        {
            var response = await httpClient.PostAsync($"api/projects/{projectName}/process/{fileName}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error processing project document layout: {ex.Message}");
            return false;
        }
    }

    public async Task<string?> GetProjectFileUrlAsync(string projectName, string fileName, bool isProcessingFile = false)
    {
        try
        {
            var encodedFileName = Uri.EscapeDataString(fileName);
            var processingParam = isProcessingFile ? "?processing=true" : "";
            return $"api/projects/{projectName}/download/{encodedFileName}{processingParam}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error generating project file URL: {ex.Message}");
            return null;
        }
    }

    public async Task<UploadDocumentsResponse> UploadFilesToProjectAsync(IReadOnlyList<IBrowserFile> files, long maxAllowedSize, string projectName, IDictionary<string, string>? metadata = null)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            foreach (var file in files)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize));
#pragma warning restore CA2000 // Dispose objects before losing scope
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                content.Add(fileContent, file.Name, file.Name);
            }

            var tokenResponse = await httpClient.GetAsync("api/token/csrf");
            tokenResponse.EnsureSuccessStatusCode();
            var token = await tokenResponse.Content.ReadAsStringAsync();
            token = token.Trim('"');

            content.Headers.Add("X-CSRF-TOKEN-FORM", token);
            content.Headers.Add("X-CSRF-TOKEN-HEADER", token);

            // Add project name to metadata
            var fileMetadata = metadata ?? new Dictionary<string, string>();
            fileMetadata["project"] = projectName;

            string serializedHeaders = System.Text.Json.JsonSerializer.Serialize(fileMetadata);
            content.Headers.Add("X-FILE-METADATA", serializedHeaders);

            var response = await httpClient.PostAsync($"api/projects/{projectName}/upload", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<UploadDocumentsResponse>();
            return result ?? UploadDocumentsResponse.FromError("Unable to upload files, unknown error.");
        }
        catch (Exception ex)
        {
            return UploadDocumentsResponse.FromError(ex.ToString());
        }
    }

    public async Task<UserInformation> GetUserAsync()
    {
        if (Cache.UserInformation != null)
            return Cache.UserInformation;

        try
        {
            var response = await httpClient.GetAsync("api/user");
            response.EnsureSuccessStatusCode();

            // sometimes the api/user call is returning an HTML error page... this crashes hard and shows you nothing...
            //var user = await response.Content.ReadFromJsonAsync<UserInformation>();

            var userInfo = await response.Content.ReadAsStringAsync();
            // if the input is invalid (i.e. error occurred...)
            if (userInfo[0..30].Contains("<html", StringComparison.CurrentCultureIgnoreCase))
            {
                return new UserInformation(false, "errorUser", "errorUser", "Call to api/user failed!", [], []);
            }

            // otherwise, if it's good - convert this into a user profile
            var user = System.Text.Json.JsonSerializer.Deserialize<UserInformation>(userInfo, SerializerOptions.Default);
            Cache.SetUserInformation(user);
            return user;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error fetching user information: {ex.Message}";
            Debug.WriteLine(errorMessage);
            return new UserInformation(false, "ERROR!", "ERROR!", errorMessage, [], []);
        }
    }
    public async Task<List<DocumentSummary>> GetUserDocumentsAsync()
    {
        var response = await httpClient.GetAsync("api/user/documents");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<DocumentSummary>>();
    }
    public async Task<List<DocumentSummary>> GetCollectionDocumentsAsync(string profileId)
    {
        var response = await httpClient.GetAsync($"api/collection/documents/{profileId}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<DocumentSummary>>();
    }
    public async Task<UserSelectionModel> GetProfileUserSelectionModelAsync(string profileId)
    {
        var response = await httpClient.GetAsync($"api/profile/selections?profileId={profileId}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UserSelectionModel>();
    }
    public async Task<UploadDocumentsResponse> UploadDocumentsAsync(IReadOnlyList<IBrowserFile> files, long maxAllowedSize, string selectedProfile, IDictionary<string, string>? metadata = null)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            foreach (var file in files)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize));
#pragma warning restore CA2000 // Dispose objects before losing scope
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                content.Add(fileContent, file.Name, file.Name);
            }

            var tokenResponse = await httpClient.GetAsync("api/token/csrf");
            tokenResponse.EnsureSuccessStatusCode();
            var token = await tokenResponse.Content.ReadAsStringAsync();
            token = token.Trim('"');

            // set token
            content.Headers.Add("X-CSRF-TOKEN-FORM", token);
            content.Headers.Add("X-CSRF-TOKEN-HEADER", token);

            if (metadata != null)
            {
                // Serialize the dictionary to a JSON string
                string serializedHeaders = System.Text.Json.JsonSerializer.Serialize(metadata);

                // Add the serialized dictionary as a single header value
                content.Headers.Add("X-FILE-METADATA", serializedHeaders);
            }

            if (selectedProfile != null)
            {
                content.Headers.Add("X-PROFILE-METADATA", selectedProfile);
            }

            var response = await httpClient.PostAsync("api/documents", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<UploadDocumentsResponse>();
            return result ?? UploadDocumentsResponse.FromError("Unable to upload files, unknown error.");
        }
        catch (Exception ex)
        {
            return UploadDocumentsResponse.FromError(ex.ToString());
        }
    }
    public async Task<DocumentIndexResponse> NativeIndexDocumentsAsync(UploadDocumentsResponse documentList) // DocumentIndexRequest indexRequest)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(documentList, SerializerOptions.Default);
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("api/native/index/documents", body);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<DocumentIndexResponse>();
            return result ?? DocumentIndexResponse.FromError("Unable to index files, unknown error.");
        }
        catch (Exception ex)
        {
            return DocumentIndexResponse.FromError(ex.ToString());
        }
    }
    public async IAsyncEnumerable<DocumentSummary> GetDocumentsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/user/documents", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var options = SerializerOptions.Default;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            await foreach (var document in System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<DocumentSummary>(stream, options, cancellationToken))
            {
                if (document is null)
                {
                    continue;
                }

                yield return document;
            }
        }
    }
    public async IAsyncEnumerable<ChatHistoryResponse> GetFeedbackAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/feedback", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var options = SerializerOptions.Default;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            await foreach (var document in System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<ChatHistoryResponse>(stream, options, cancellationToken))
            {
                if (document is null)
                {
                    continue;
                }

                yield return document;
            }
        }
    }
    public async IAsyncEnumerable<ChatHistoryResponse> GetHistoryAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/chat/history", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var options = SerializerOptions.Default;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await foreach (var document in System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<ChatHistoryResponse>(stream, options, cancellationToken))
            {
                if (document is null)
                {
                    continue;
                }

                yield return document;
            }
        }
    }
    public async IAsyncEnumerable<ChatSessionModel> GetHistoryV2Async([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/chat/history-v2", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var options = SerializerOptions.Default;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await foreach (var session in System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<ChatSessionModel>(stream, options, cancellationToken))
            {
                if (session is null)
                {
                    continue;
                }

                yield return session;
            }
        }
    }
    public async IAsyncEnumerable<ChatHistoryResponse> GetChatHistorySessionAsync([EnumeratorCancellation] CancellationToken cancellationToken, string chatId)
    {
        var response = await httpClient.GetAsync($"api/chat/history/{chatId}", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var options = SerializerOptions.Default;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await foreach (var document in System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<ChatHistoryResponse>(stream, options, cancellationToken))
            {
                if (document is null)
                {
                    continue;
                }

                yield return document;
            }
        }
    }
    public async Task ChatRatingAsync(ChatRatingRequest request)
    {
        await PostBasicAsync(request, "api/chat/rating");
    }

    public async Task<VoiceLiveTokenResponse?> GetVoiceLiveTokenAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("api/voicelive/token");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<VoiceLiveTokenResponse>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching Voice Live token: {ex.Message}");
            return null;
        }
    }

    private async Task PostBasicAsync<TRequest>(TRequest request, string apiRoute)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(request, SerializerOptions.Default);
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(apiRoute, body);
    }
    public async Task<(ProfileInfo, string)> GetProfilesInfoAsync()
    {
        return await LoadProfilesAsync("api/profiles/info");
    }
    public async Task<(ProfileInfo, string)> GetProfilesReloadAsync()
    {
        return await LoadProfilesAsync("api/profiles/reload");
    }
    private async Task<(ProfileInfo, string)> LoadProfilesAsync(string apiToCall)
    {
       var profileInfo = new ProfileInfo();
       var rawJson = string.Empty;
       var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, IncludeFields = true, WriteIndented = true };
        try
        {
            profileInfo = await httpClient.GetFromJsonAsync<ProfileInfo>(apiToCall, options: jsonOptions);
            rawJson = System.Text.Json.JsonSerializer.Serialize(profileInfo, jsonOptions);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error reading profile info! {ex.Message}";
            Debug.WriteLine(errorMsg);
            profileInfo = new ProfileInfo();
            rawJson = errorMsg;
        }
        return (profileInfo!, rawJson);
    }
    public async IAsyncEnumerable<ChatChunkResponse> StreamChatAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat/streaming")
        {
            Headers = { { "Accept", "application/json" } },
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
        };
        httpRequest.SetBrowserResponseStreamingEnabled(true);
        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (var chunk in System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<ChatChunkResponse>(responseStream, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, DefaultBufferSize = 32 }, cancellationToken))
        {
            if (chunk == null)
                continue;
            yield return chunk;
        }
    }
}
