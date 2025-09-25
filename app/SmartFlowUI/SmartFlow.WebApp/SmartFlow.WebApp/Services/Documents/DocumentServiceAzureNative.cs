// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Services.ChatHistory;

public class DocumentServiceAzureNative : IDocumentService
{
    private readonly CosmosClient _cosmosClient;
    private readonly ProfileService _profileService;
    private readonly Container _cosmosContainer;
    private readonly AzureBlobStorageService _blobStorageService;
    private readonly HttpClient _httpClient;
    private readonly SearchClientFactory _searchClientFactory;

    public DocumentServiceAzureNative(CosmosClient cosmosClient, AzureBlobStorageService blobStorageService, HttpClient httpClient,SearchClientFactory searchClientFactory, ProfileService profileService)
    {
        _cosmosClient = cosmosClient;
        _profileService = profileService;
        _blobStorageService = blobStorageService;
        _searchClientFactory = searchClientFactory;

        // Create database if it doesn't exist
        try
        {
            var db = _cosmosClient.CreateDatabaseIfNotExistsAsync(DefaultSettings.CosmosDbDatabaseName).GetAwaiter().GetResult();
            // Create get container if it doesn't exist
            _cosmosContainer = db.Database.CreateContainerIfNotExistsAsync(DefaultSettings.CosmosDBUserDocumentsCollectionName, "/userId").GetAwaiter().GetResult();
        }
        catch (CosmosException ex) {
            if (ex.StatusCode == HttpStatusCode.Forbidden && ex.Message.Contains("firewall settings", StringComparison.InvariantCultureIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"==> Connection to Cosmos {_cosmosClient.Endpoint.Host} failed because of a misconfigured firewall setting!");
                // Message might contain this...: "code":"Forbidden","message":"Request originated from IP x.x.188.38 through public internet. This is blocked by your Cosmos DB account firewall settings. More info: https:...
                var startLoc = ex.Message.IndexOf("\"code\":\"Forbidden\",\"message\":\"", StringComparison.InvariantCultureIgnoreCase);
                var endLoc = ex.Message.IndexOf("More info:", startLoc + 30, StringComparison.InvariantCultureIgnoreCase);
                if (startLoc > 0 && endLoc > 0)
                {
                    var ipMessage = ex.Message.Substring(startLoc + 30, endLoc - startLoc - 30);
                    Console.WriteLine($"==> {ipMessage}");
                }
                Console.ResetColor();
            }
            //throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"*** Connection to Cosmos failed: {ex.Message}");
            throw;
        }
    }

    public async Task<UploadDocumentsResponse> CreateDocumentUploadAsync(UserInformation userInfo, IFormFileCollection files, string selectedProfile, Dictionary<string, string>? fileMetadata, CancellationToken cancellationToken)
    {
        var profileInfo = await _profileService.GetProfileDataAsync();
        var selectedProfileDefinition = profileInfo.Profiles.First(p => p.Id == selectedProfile);

        if (selectedProfileDefinition.RAGSettings == null)
        {
            throw new ArgumentException($"Profile {selectedProfile} not found or RAGSettings not set.");
        }

        var indexName = selectedProfileDefinition.RAGSettings.DocumentRetrievalIndexName;
        var metadata = string.Join(",", fileMetadata.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        var response = await _blobStorageService.UploadFilesV2Async(userInfo, files, selectedProfile, fileMetadata, cancellationToken);

        var searchIndexerClient = _searchClientFactory.GetSearchIndexerClient();
        var task = searchIndexerClient.RunIndexerAsync(selectedProfileDefinition.RAGSettings.DocumentIndexerName);
        task.Wait(5000);

        foreach (var file in response.UploadedFiles)
        {
            await CreateDocumentUploadAsync(userInfo, file, indexName, selectedProfileDefinition.Id, metadata);
        }
        return response;
    }

    private async Task CreateDocumentUploadAsync(UserInformation user, UploadDocumentFileSummary fileSummary, string indexName, string profileId, string metadata, string contentType = "application/pdf")
    {
        var document = new DocumentUpload(Guid.NewGuid().ToString(), user.UserId, fileSummary.FileName, fileSummary.FileName, contentType, fileSummary.Size, indexName, profileId, DocumentProcessingStatus.Succeeded, metadata);
        await _cosmosContainer.CreateItemAsync(document, partitionKey: new PartitionKey(document.UserId));
    }

    public async Task<List<DocumentUpload>> GetDocumentUploadsAsync(UserInformation user, string profileId)
    {
        var results = new List<DocumentUpload>();
        if (_cosmosContainer != null)
        {
            var query = _cosmosContainer.GetItemQueryIterator<DocumentUpload>(
                new QueryDefinition("SELECT TOP 100 * FROM c WHERE c.sessionId = @sessionId ORDER BY c.sourceName DESC")
                .WithParameter("@username", user.UserId)
                .WithParameter("@sessionId", profileId));
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response.ToList());
            }
        }
        return results;
    }
}
