// Copyright (c) Microsoft. All rights reserved.

using Shared.Json;
using Shared.Models;

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
                containerItem.Properties.Metadata.TryGetValue("managedcollection", out var value) &&
                value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
            {
                collections.Add(containerItem.Name);
            }
        }

        return collections;
    }

    /// <summary>
    /// Adds the "managed-collection": "true" metadata tag to the specified container.
    /// Creates the container if it doesn't exist.
    /// </summary>
    /// <param name="containerName">The name of the container to tag</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> AddManagedCollectionTagAsync(string containerName, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // Create the container if it doesn't exist
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                await containerClient.CreateAsync(cancellationToken: cancellationToken);
            }

            // Get current metadata (if container already existed, preserve existing metadata)
            var properties = await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var metadata = properties.Value.Metadata ?? new Dictionary<string, string>();
            
            // Add or update the managed-collection tag
            metadata["managedcollection"] = "true";
            
            await containerClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all files (blobs) in the specified container along with their associated processing files
    /// </summary>
    /// <param name="containerName">The name of the container</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of container file information including main files and their processing files</returns>
    public async Task<List<ContainerFileInfo>> GetFilesInContainerAsync(string containerName, CancellationToken cancellationToken = default)
    {
        var files = new List<ContainerFileInfo>();
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        if (!await containerClient.ExistsAsync(cancellationToken))
        {
            return files;
        }

        // Get the extract container client
        var extractContainerName = $"{containerName}-extract";
        var extractContainerClient = _blobServiceClient.GetBlobContainerClient(extractContainerName);
        var extractContainerExists = await extractContainerClient.ExistsAsync(cancellationToken);

        await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            var fileInfo = new ContainerFileInfo(blobItem.Name);

            // Check for processing files in the extract container
            if (extractContainerExists)
            {
                var baseNameWithoutExtension = Path.GetFileNameWithoutExtension(blobItem.Name);
                var directoryPath = Path.GetDirectoryName(blobItem.Name);
                
                // Look for files with the same base name in the extract container
                var searchPrefix = string.IsNullOrEmpty(directoryPath) 
                    ? baseNameWithoutExtension 
                    : $"{directoryPath}/{baseNameWithoutExtension}";

                await foreach (var extractBlobItem in extractContainerClient.GetBlobsAsync(prefix: searchPrefix, cancellationToken: cancellationToken))
                {
                    var extractBaseName = Path.GetFileNameWithoutExtension(extractBlobItem.Name);
                    var originalBaseName = Path.GetFileNameWithoutExtension(blobItem.Name);
                    fileInfo.ProcessingFiles.Add(extractBlobItem.Name);
                }
            }

            files.Add(fileInfo);
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

    /// <summary>
    /// Deletes a file from a specific container and its associated processing files
    /// </summary>
    /// <param name="containerName">The name of the container</param>
    /// <param name="fileName">The name of the file to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file was deleted successfully, false otherwise</returns>
    public async Task<bool> DeleteFileFromContainerAsync(
        string containerName, 
        string fileName, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return false;
            }

            var blobClient = containerClient.GetBlobClient(fileName);
            
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return false;
            }

            // Delete the main file
            await blobClient.DeleteAsync(cancellationToken: cancellationToken);

            // Delete associated processing files from the extract container
            var extractContainerName = $"{containerName}-extract";
            var extractContainerClient = _blobServiceClient.GetBlobContainerClient(extractContainerName);
            var extractContainerExists = await extractContainerClient.ExistsAsync(cancellationToken);

            if (extractContainerExists)
            {
                var baseNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                var directoryPath = Path.GetDirectoryName(fileName);
                
                // Look for files with the same base name in the extract container
                var searchPrefix = string.IsNullOrEmpty(directoryPath) 
                    ? baseNameWithoutExtension 
                    : $"{directoryPath}/{baseNameWithoutExtension}";

                await foreach (var extractBlobItem in extractContainerClient.GetBlobsAsync(prefix: searchPrefix, cancellationToken: cancellationToken))
                {
                    var extractBlobClient = extractContainerClient.GetBlobClient(extractBlobItem.Name);
                    await extractBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads a file from a specific container
    /// </summary>
    /// <param name="containerName">The name of the container</param>
    /// <param name="fileName">The name of the file to download</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing the file stream and content type</returns>
    public async Task<(Stream? stream, string contentType)> DownloadFileAsync(
        string containerName, 
        string fileName, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return (null, string.Empty);
            }

            var blobClient = containerClient.GetBlobClient(fileName);
            
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return (null, string.Empty);
            }

            var download = await blobClient.DownloadAsync(cancellationToken);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            
            var contentType = properties.Value.ContentType;
            
            // Set appropriate content type based on file extension if not already set
            if (string.IsNullOrEmpty(contentType))
            {
                contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
                {
                    ".pdf" => "application/pdf",
                    ".md" => "text/markdown",
                    ".txt" => "text/plain",
                    ".json" => "application/json",
                    _ => "application/octet-stream"
                };
            }

            return (download.Value.Content, contentType);
        }
        catch
        {
            return (null, string.Empty);
        }
    }

    public async Task<List<DocumentUpload>> GetDocumentUploadsAsync(UserInformation user, string? profileId = null)
    {
        return null;
    }
}
