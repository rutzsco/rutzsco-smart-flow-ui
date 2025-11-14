// Copyright (c) Microsoft. All rights reserved.

using Shared.Models;

namespace MinimalApi.Services.ChatHistory;

public class ProjectService
{
    private const string ProjectContainerName = "project-files";
    private const string ProjectExtractContainerName = "project-files-extract";
    private const string ProjectMetadataContainerName = "project-metadata";
    
    private readonly AzureBlobStorageService _blobStorageService;
    private readonly BlobServiceClient _blobServiceClient;

    public ProjectService(AzureBlobStorageService blobStorageService, BlobServiceClient blobServiceClient)
    {
        _blobStorageService = blobStorageService;
        _blobServiceClient = blobServiceClient;
    }

    /// <summary>
    /// Gets all projects from the project metadata container
    /// </summary>
    public async Task<List<CollectionInfo>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = new List<CollectionInfo>();
        var containerClient = _blobServiceClient.GetBlobContainerClient(ProjectMetadataContainerName);

        if (!await containerClient.ExistsAsync(cancellationToken))
        {
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            return projects;
        }

        await foreach (var blobItem in containerClient.GetBlobsAsync(traits: BlobTraits.Metadata, cancellationToken: cancellationToken))
        {
            var projectName = Path.GetFileNameWithoutExtension(blobItem.Name);
            var description = blobItem.Metadata?.TryGetValue("description", out var desc) == true ? desc : null;
            var type = blobItem.Metadata?.TryGetValue("type", out var typeValue) == true ? typeValue : null;
            
            projects.Add(new CollectionInfo(projectName, description, type));
        }

        return projects;
    }

    /// <summary>
    /// Creates a new project by adding a metadata blob
    /// </summary>
    public async Task<bool> CreateProjectAsync(string projectName, string? description = null, string? type = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ProjectMetadataContainerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobName = $"{projectName}.json";
            var blobClient = containerClient.GetBlobClient(blobName);

            // Check if project already exists
            if (await blobClient.ExistsAsync(cancellationToken))
            {
                return false;
            }

            // Create metadata blob
            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(description))
                metadata["description"] = description;
            if (!string.IsNullOrWhiteSpace(type))
                metadata["type"] = type;

            var projectInfo = new { name = projectName, description, type, createdDate = DateTime.UtcNow };
            var json = System.Text.Json.JsonSerializer.Serialize(projectInfo);
            
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = "application/json" }, metadata, cancellationToken: cancellationToken);

            // Ensure main container exists
            var projectContainerClient = _blobServiceClient.GetBlobContainerClient(ProjectContainerName);
            await projectContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes a project and all its files
    /// </summary>
    public async Task<bool> DeleteProjectAsync(string projectName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Delete the metadata blob
            var metadataContainerClient = _blobServiceClient.GetBlobContainerClient(ProjectMetadataContainerName);
            if (await metadataContainerClient.ExistsAsync(cancellationToken))
            {
                var blobName = $"{projectName}.json";
                var blobClient = metadataContainerClient.GetBlobClient(blobName);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }

            // Delete all files tagged with this project
            var containerClient = _blobServiceClient.GetBlobContainerClient(ProjectContainerName);
            if (await containerClient.ExistsAsync(cancellationToken))
            {
                await foreach (var blobItem in containerClient.GetBlobsAsync(traits: BlobTraits.Metadata, cancellationToken: cancellationToken))
                {
                    if (blobItem.Metadata?.TryGetValue("project", out var project) == true && project == projectName)
                    {
                        var blobClient = containerClient.GetBlobClient(blobItem.Name);
                        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                    }
                }
            }

            // Delete all processing files for this project
            var extractContainerClient = _blobServiceClient.GetBlobContainerClient(ProjectExtractContainerName);
            if (await extractContainerClient.ExistsAsync(cancellationToken))
            {
                await foreach (var blobItem in extractContainerClient.GetBlobsAsync(traits: BlobTraits.Metadata, cancellationToken: cancellationToken))
                {
                    if (blobItem.Metadata?.TryGetValue("project", out var project) == true && project == projectName)
                    {
                        var blobClient = extractContainerClient.GetBlobClient(blobItem.Name);
                        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                    }
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
    /// Updates project metadata
    /// </summary>
    public async Task<bool> UpdateProjectMetadataAsync(string projectName, string? description = null, string? type = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ProjectMetadataContainerName);
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return false;
            }

            var blobName = $"{projectName}.json";
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return false;
            }

            // Update metadata
            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(description))
                metadata["description"] = description;
            if (!string.IsNullOrWhiteSpace(type))
                metadata["type"] = type;

            await blobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);

            // Update blob content
            var projectInfo = new { name = projectName, description, type, updatedDate = DateTime.UtcNow };
            var json = System.Text.Json.JsonSerializer.Serialize(projectInfo);
            
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets project metadata
    /// </summary>
    public async Task<CollectionInfo?> GetProjectMetadataAsync(string projectName, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ProjectMetadataContainerName);
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return null;
            }

            var blobName = $"{projectName}.json";
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return null;
            }

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var metadata = properties.Value.Metadata;

            var description = metadata?.TryGetValue("description", out var desc) == true ? desc : null;
            var type = metadata?.TryGetValue("type", out var typeValue) == true ? typeValue : null;

            return new CollectionInfo(projectName, description, type);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all files for a specific project
    /// </summary>
    public async Task<List<ContainerFileInfo>> GetProjectFilesAsync(string projectName, CancellationToken cancellationToken = default)
    {
        var files = new List<ContainerFileInfo>();
        var containerClient = _blobServiceClient.GetBlobContainerClient(ProjectContainerName);

        if (!await containerClient.ExistsAsync(cancellationToken))
        {
            return files;
        }

        var extractContainerClient = _blobServiceClient.GetBlobContainerClient(ProjectExtractContainerName);
        var extractContainerExists = await extractContainerClient.ExistsAsync(cancellationToken);

        await foreach (var blobItem in containerClient.GetBlobsAsync(traits: BlobTraits.Metadata, cancellationToken: cancellationToken))
        {
            // Filter by project metadata
            if (blobItem.Metadata?.TryGetValue("project", out var project) == true && project == projectName)
            {
                var fileInfo = new ContainerFileInfo(blobItem.Name);

                // Check for processing files
                if (extractContainerExists)
                {
                    var baseNameWithoutExtension = Path.GetFileNameWithoutExtension(blobItem.Name);
                    
                    await foreach (var extractBlobItem in extractContainerClient.GetBlobsAsync(traits: BlobTraits.Metadata, cancellationToken: cancellationToken))
                    {
                        if (extractBlobItem.Metadata?.TryGetValue("project", out var extractProject) == true && 
                            extractProject == projectName &&
                            extractBlobItem.Name.StartsWith(baseNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            fileInfo.ProcessingFiles.Add(extractBlobItem.Name);
                        }
                    }
                }

                files.Add(fileInfo);
            }
        }

        return files;
    }

    /// <summary>
    /// Uploads files to a project with project metadata tag
    /// </summary>
    public async Task<UploadDocumentsResponse> UploadFilesToProjectAsync(
        UserInformation userInfo, 
        IFormFileCollection files, 
        string projectName,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var fileMetadata = metadata ?? new Dictionary<string, string>();
        fileMetadata["project"] = projectName;
        
        var response = await _blobStorageService.UploadFilesAsync(userInfo, files, ProjectContainerName, fileMetadata, cancellationToken);
        return response;
    }

    /// <summary>
    /// Deletes a file from a project
    /// </summary>
    public async Task<bool> DeleteFileFromProjectAsync(string projectName, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ProjectContainerName);
            
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return false;
            }

            var blobClient = containerClient.GetBlobClient(fileName);
            
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return false;
            }

            // Verify the blob belongs to this project
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (properties.Value.Metadata?.TryGetValue("project", out var project) != true || project != projectName)
            {
                return false;
            }

            // Delete the main file
            await blobClient.DeleteAsync(cancellationToken: cancellationToken);

            // Delete associated processing files
            var extractContainerClient = _blobServiceClient.GetBlobContainerClient(ProjectExtractContainerName);
            var extractContainerExists = await extractContainerClient.ExistsAsync(cancellationToken);

            if (extractContainerExists)
            {
                var baseNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

                await foreach (var extractBlobItem in extractContainerClient.GetBlobsAsync(traits: BlobTraits.Metadata, cancellationToken: cancellationToken))
                {
                    if (extractBlobItem.Metadata?.TryGetValue("project", out var extractProject) == true && 
                        extractProject == projectName &&
                        extractBlobItem.Name.StartsWith(baseNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        var extractBlobClient = extractContainerClient.GetBlobClient(extractBlobItem.Name);
                        await extractBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                    }
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
    /// Downloads a file from a project
    /// </summary>
    public async Task<(Stream? stream, string contentType)> DownloadFileAsync(
        string projectName, 
        string fileName, 
        bool isProcessingFile = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerName = isProcessingFile ? ProjectExtractContainerName : ProjectContainerName;
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

            // Verify the blob belongs to this project
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (properties.Value.Metadata?.TryGetValue("project", out var project) != true || project != projectName)
            {
                return (null, string.Empty);
            }

            var download = await blobClient.DownloadAsync(cancellationToken);
            var contentType = properties.Value.ContentType;
            
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
}
