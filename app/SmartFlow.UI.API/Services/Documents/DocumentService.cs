// Copyright (c) Microsoft. All rights reserved.

using Shared.Json;

namespace MinimalApi.Services.ChatHistory;

public class DocumentService
{
    private readonly AzureBlobStorageService _blobStorageService;
    private readonly AppConfiguration _configuration;
    private readonly BlobServiceClient _blobServiceClient;

    public DocumentService(AzureBlobStorageService blobStorageService, AppConfiguration configuration, BlobServiceClient blobServiceClient)
    {
        _blobStorageService = blobStorageService;
        _configuration = configuration;
        _blobServiceClient = blobServiceClient;
    }

    public async Task<UploadDocumentsResponse> CreateDocumentUploadAsync(UserInformation userInfo, IFormFileCollection files, string selectedProfile, Dictionary<string, string>? fileMetadata, CancellationToken cancellationToken)
    {
        var response = await _blobStorageService.UploadFilesAsync(userInfo, files, selectedProfile, new Dictionary<string, string>(), cancellationToken);
        return response;
    }

    /// <summary>
    /// Gets all blob storage containers that have the metadata tag "managed-collection" set to "true"
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of container names that are managed collections</returns>
    public async Task<List<string>> GetCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var collections = new List<string>();
        
        await foreach (var containerItem in _blobServiceClient.GetBlobContainersAsync(BlobContainerTraits.Metadata, cancellationToken: cancellationToken))
        {
            if (containerItem.Properties?.Metadata != null &&
                containerItem.Properties.Metadata.TryGetValue("managed-collection", out var value) &&
                value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
            {
                collections.Add(containerItem.Name);
            }
        }

        return collections;
    }

    /// <summary>
    /// Adds the "managed-collection": "true" metadata tag to the specified container
    /// </summary>
    /// <param name="containerName">The name of the container to tag</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> AddManagedCollectionTagAsync(string containerName, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return false;
            }

            var properties = await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var metadata = properties.Value.Metadata ?? new Dictionary<string, string>();
            
            metadata["managed-collection"] = "true";
            
            await containerClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all files (blobs) in the specified container
    /// </summary>
    /// <param name="containerName">The name of the container</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of blob names in the container</returns>
    public async Task<List<string>> GetFilesInContainerAsync(string containerName, CancellationToken cancellationToken = default)
    {
        var files = new List<string>();
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        if (!await containerClient.ExistsAsync(cancellationToken))
        {
            return files;
        }

        await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            files.Add(blobItem.Name);
        }

        return files;
    }

    /// <summary>
    /// Uploads files to a specific container using the blob storage service
    /// </summary>
    /// <param name="userInfo">User information</param>
    /// <param name="files">Files to upload</param>
    /// <param name="containerName">Target container name</param>
    /// <param name="metadata">Optional metadata for the files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload response with details of uploaded files</returns>
    public async Task<UploadDocumentsResponse> UploadFilesToContainerAsync(
        UserInformation userInfo, 
        IFormFileCollection files, 
        string containerName, 
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var fileMetadata = metadata ?? new Dictionary<string, string>();
        var response = await _blobStorageService.UploadFilesAsync(userInfo, files, containerName, fileMetadata, cancellationToken);
        return response;
    }

    public async Task<List<DocumentUpload>> GetDocumentUploadsAsync(UserInformation user, string? profileId = null)
    {
        return null;
    }
}
