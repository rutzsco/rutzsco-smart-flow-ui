// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Extensions;

internal static class WebApiCollectionEndpoints
{
    internal static WebApplication MapCollectionApi(this WebApplication app)
    {
        var api = app.MapGroup("api/collections");

        // Get all managed collections
        api.MapGet("", OnGetCollectionsAsync);

        // Add managed collection tag to a container
        api.MapPost("{containerName}/tag", OnAddManagedCollectionTagAsync);

        // Get all files in a container
        api.MapGet("{containerName}/files", OnGetFilesInContainerAsync);

        // Upload files to a specific container
        api.MapPost("{containerName}/upload", OnUploadFilesToContainerAsync);

        // Download a file from a container
        api.MapGet("{containerName}/download/{*fileName}", OnDownloadFileAsync);

        return app;
    }

    private static async Task<IResult> OnGetCollectionsAsync(
        HttpContext context,
        [FromServices] DocumentService documentService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting managed collections");
            
            var userInfo = await context.GetUserInfoAsync();
            var collections = await documentService.GetCollectionsAsync(cancellationToken);

            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";

            return TypedResults.Ok(collections);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting managed collections");
            return Results.Problem("Error retrieving managed collections");
        }
    }

    private static async Task<IResult> OnAddManagedCollectionTagAsync(
        HttpContext context,
        string containerName,
        [FromServices] DocumentService documentService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Creating/tagging managed collection: {ContainerName}", containerName);

            var userInfo = await context.GetUserInfoAsync();
            var success = await documentService.AddManagedCollectionTagAsync(containerName, cancellationToken);

            if (success)
            {
                logger.LogInformation("Successfully created/tagged container '{ContainerName}' as managed collection", containerName);
                return TypedResults.Ok(new { success = true, message = $"Collection '{containerName}' created successfully" });
            }
            else
            {
                logger.LogWarning("Failed to create/tag container '{ContainerName}'", containerName);
                return Results.BadRequest(new { success = false, message = $"Failed to create collection '{containerName}'" });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating/tagging managed collection: {ContainerName}", containerName);
            return Results.Problem("Error creating collection");
        }
    }

    private static async Task<IResult> OnGetFilesInContainerAsync(
        HttpContext context,
        string containerName,
        [FromServices] DocumentService documentService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting files from container: {ContainerName}", containerName);

            var userInfo = await context.GetUserInfoAsync();
            var files = await documentService.GetFilesInContainerAsync(containerName, cancellationToken);

            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";

            return TypedResults.Ok(files);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting files from container: {ContainerName}", containerName);
            return Results.Problem("Error retrieving files from container");
        }
    }

    private static async Task<IResult> OnUploadFilesToContainerAsync(
        HttpContext context,
        string containerName,
        [FromForm] IFormFileCollection files,
        [FromServices] DocumentService documentService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Uploading {FileCount} files to container: {ContainerName}", files.Count, containerName);

            var userInfo = await context.GetUserInfoAsync();
            
            // Read optional metadata from headers
            var fileMetadataContent = context.Request.Headers["X-FILE-METADATA"];
            Dictionary<string, string>? fileMetadata = null;
            if (!string.IsNullOrEmpty(fileMetadataContent))
            {
                fileMetadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(fileMetadataContent);
            }

            var response = await documentService.UploadFilesToContainerAsync(
                userInfo, 
                files, 
                containerName, 
                fileMetadata, 
                cancellationToken);

            logger.LogInformation("Upload to container '{ContainerName}' completed: {Response}", containerName, response);

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading files to container: {ContainerName}", containerName);
            return Results.Problem("Error uploading files to container");
        }
    }

    private static async Task<IResult> OnDownloadFileAsync(
        HttpContext context,
        string containerName,
        string fileName,
        [FromServices] DocumentService documentService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // URL decode the filename since it comes URL-encoded from the client
            fileName = Uri.UnescapeDataString(fileName);
            
            logger.LogInformation("Downloading file {FileName} from container: {ContainerName}", fileName, containerName);

            var userInfo = await context.GetUserInfoAsync();
            var (stream, contentType) = await documentService.DownloadFileAsync(containerName, fileName, cancellationToken);

            if (stream == null)
            {
                return Results.NotFound(new { message = $"File '{fileName}' not found in container '{containerName}'" });
            }

            var fileNameOnly = Path.GetFileName(fileName);
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            // Ensure content type is set correctly
            if (string.IsNullOrEmpty(contentType) || contentType == "application/octet-stream")
            {
                contentType = extension switch
                {
                    ".pdf" => "application/pdf",
                    ".md" => "text/markdown",
                    ".txt" => "text/plain",
                    ".json" => "application/json",
                    _ => "application/octet-stream"
                };
            }
            
            // For PDFs, explicitly set headers for inline viewing
            if (extension == ".pdf")
            {
                context.Response.Headers["Content-Type"] = "application/pdf";
                context.Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileNameOnly}\"";
                context.Response.Headers["Accept-Ranges"] = "bytes";
                context.Response.Headers["Cache-Control"] = "public, max-age=3600";
                
                return Results.Stream(stream, contentType: "application/pdf", enableRangeProcessing: true);
            }
            
            // For Markdown and JSON files, use inline disposition so they can be viewed in browser
            // For other files, use attachment disposition to trigger download
            var enableRangeProcessing = extension == ".pdf";
            var fileDownloadName = (extension == ".pdf" || extension == ".md" || extension == ".json") ? null : fileNameOnly;
            
            if (fileDownloadName == null)
            {
                context.Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileNameOnly}\"";
            }
            
            return Results.Stream(
                stream, 
                contentType: contentType, 
                fileDownloadName: fileDownloadName, 
                enableRangeProcessing: enableRangeProcessing);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading file {FileName} from container: {ContainerName}", fileName, containerName);
            return Results.Problem("Error downloading file from container");
        }
    }
}
