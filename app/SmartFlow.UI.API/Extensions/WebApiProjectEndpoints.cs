// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Extensions;

internal static class WebApiProjectEndpoints
{
    private const string ProjectContainerName = "project-files";

    internal static WebApplication MapProjectApi(this WebApplication app)
    {
        var api = app.MapGroup("api/projects");

        // Get all projects
        api.MapGet("", OnGetProjectsAsync);

        // Create a new project
        api.MapPost("", OnCreateProjectAsync);

        // Delete a project
        api.MapDelete("{projectName}", OnDeleteProjectAsync);

        // Update project metadata
        api.MapPut("{projectName}/metadata", OnUpdateProjectMetadataAsync);

        // Get project metadata
        api.MapGet("{projectName}/metadata", OnGetProjectMetadataAsync);

        // Get all files in a project
        api.MapGet("{projectName}/files", OnGetFilesInProjectAsync);

        // Upload files to a specific project
        api.MapPost("{projectName}/upload", OnUploadFilesToProjectAsync);

        // Download a file from a project
        api.MapGet("{projectName}/download/{*fileName}", OnDownloadProjectFileAsync);

        // Delete a file from a project
        api.MapDelete("{projectName}/files/{*fileName}", OnDeleteProjectFileAsync);

        return app;
    }

    private static async Task<IResult> OnGetProjectsAsync(
        HttpContext context,
        [FromServices] ProjectService projectService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting projects");
            
            var userInfo = await context.GetUserInfoAsync();
            var projects = await projectService.GetProjectsAsync(cancellationToken);

            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";

            return TypedResults.Ok(projects);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting projects");
            return Results.Problem("Error retrieving projects");
        }
    }

    private static async Task<IResult> OnCreateProjectAsync(
        HttpContext context,
        [FromBody] CreateCollectionRequest request,
        [FromServices] ProjectService projectService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Creating project: {ProjectName} with description: {Description}, type: {Type}", 
                request.Name, request.Description, request.Type);

            var userInfo = await context.GetUserInfoAsync();
            var success = await projectService.CreateProjectAsync(
                request.Name, 
                request.Description, 
                request.Type, 
                cancellationToken);

            if (success)
            {
                logger.LogInformation("Successfully created project '{ProjectName}'", request.Name);
                return TypedResults.Ok(new { success = true, message = $"Project '{request.Name}' created successfully" });
            }
            else
            {
                logger.LogWarning("Failed to create project '{ProjectName}'", request.Name);
                return Results.BadRequest(new { success = false, message = $"Failed to create project '{request.Name}'" });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating project: {ProjectName}", request.Name);
            return Results.Problem("Error creating project");
        }
    }

    private static async Task<IResult> OnDeleteProjectAsync(
        HttpContext context,
        string projectName,
        [FromServices] ProjectService projectService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Deleting project: {ProjectName}", projectName);

            var userInfo = await context.GetUserInfoAsync();
            var success = await projectService.DeleteProjectAsync(projectName, cancellationToken);

            if (success)
            {
                logger.LogInformation("Successfully deleted project '{ProjectName}'", projectName);
                return TypedResults.Ok(new { success = true, message = $"Project '{projectName}' deleted successfully" });
            }
            else
            {
                logger.LogWarning("Failed to delete project '{ProjectName}'", projectName);
                return Results.NotFound(new { success = false, message = $"Project '{projectName}' not found" });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting project: {ProjectName}", projectName);
            return Results.Problem("Error deleting project");
        }
    }

    private static async Task<IResult> OnUpdateProjectMetadataAsync(
        HttpContext context,
        string projectName,
        [FromBody] CreateCollectionRequest request,
        [FromServices] ProjectService projectService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Updating metadata for project: {ProjectName}", projectName);

            var userInfo = await context.GetUserInfoAsync();
            var success = await projectService.UpdateProjectMetadataAsync(
                projectName, 
                request.Description, 
                request.Type, 
                cancellationToken);

            if (success)
            {
                logger.LogInformation("Successfully updated metadata for project '{ProjectName}'", projectName);
                return TypedResults.Ok(new { success = true, message = $"Project '{projectName}' metadata updated successfully" });
            }
            else
            {
                logger.LogWarning("Failed to update metadata for project '{ProjectName}'", projectName);
                return Results.NotFound(new { success = false, message = $"Project '{projectName}' not found" });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating metadata for project: {ProjectName}", projectName);
            return Results.Problem("Error updating project metadata");
        }
    }

    private static async Task<IResult> OnGetProjectMetadataAsync(
        HttpContext context,
        string projectName,
        [FromServices] ProjectService projectService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting metadata for project: {ProjectName}", projectName);

            var userInfo = await context.GetUserInfoAsync();
            var metadata = await projectService.GetProjectMetadataAsync(projectName, cancellationToken);

            if (metadata != null)
            {
                return TypedResults.Ok(metadata);
            }
            else
            {
                return Results.NotFound(new { message = $"Project '{projectName}' not found" });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting metadata for project: {ProjectName}", projectName);
            return Results.Problem("Error retrieving project metadata");
        }
    }

    private static async Task<IResult> OnGetFilesInProjectAsync(
        HttpContext context,
        string projectName,
        [FromServices] ProjectService projectService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting files from project: {ProjectName}", projectName);

            var userInfo = await context.GetUserInfoAsync();
            var files = await projectService.GetProjectFilesAsync(projectName, cancellationToken);

            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";

            return TypedResults.Ok(files);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting files from project: {ProjectName}", projectName);
            return Results.Problem("Error retrieving files from project");
        }
    }

    private static async Task<IResult> OnUploadFilesToProjectAsync(
        HttpContext context,
        string projectName,
        [FromForm] IFormFileCollection files,
        [FromServices] ProjectService projectService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Uploading {FileCount} files to project: {ProjectName}", files.Count, projectName);

            var userInfo = await context.GetUserInfoAsync();
            
            // Read optional metadata from headers
            var fileMetadataContent = context.Request.Headers["X-FILE-METADATA"];
            Dictionary<string, string>? fileMetadata = null;
            if (!string.IsNullOrEmpty(fileMetadataContent))
            {
                fileMetadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(fileMetadataContent);
            }

            // Ensure project tag is set
            fileMetadata ??= new Dictionary<string, string>();
            fileMetadata["project"] = projectName;

            var response = await projectService.UploadFilesToProjectAsync(
                userInfo, 
                files, 
                projectName,
                fileMetadata, 
                cancellationToken);

            logger.LogInformation("Upload to project '{ProjectName}' completed: {Response}", projectName, response);

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading files to project: {ProjectName}", projectName);
            return Results.Problem("Error uploading files to project");
        }
    }

    private static async Task<IResult> OnDeleteProjectFileAsync(
        HttpContext context,
        string projectName,
        string fileName,
        [FromServices] ProjectService projectService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            fileName = Uri.UnescapeDataString(fileName);
            
            logger.LogInformation("Deleting file {FileName} from project: {ProjectName}", fileName, projectName);

            var userInfo = await context.GetUserInfoAsync();
            var success = await projectService.DeleteFileFromProjectAsync(projectName, fileName, cancellationToken);

            if (success)
            {
                logger.LogInformation("Successfully deleted file '{FileName}' from project '{ProjectName}'", fileName, projectName);
                return TypedResults.Ok(new { success = true, message = $"File '{fileName}' deleted successfully" });
            }
            else
            {
                logger.LogWarning("Failed to delete file '{FileName}' from project '{ProjectName}'", fileName, projectName);
                return Results.NotFound(new { success = false, message = $"File '{fileName}' not found" });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting file {FileName} from project: {ProjectName}", fileName, projectName);
            return Results.Problem("Error deleting file from project");
        }
    }

    private static async Task<IResult> OnDownloadProjectFileAsync(
        HttpContext context,
        string projectName,
        string fileName,
        [FromServices] ProjectService projectService,
        [FromServices] ILogger<WebApplication> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            fileName = Uri.UnescapeDataString(fileName);
            var isProcessingFile = context.Request.Query.ContainsKey("processing");
            
            logger.LogInformation("Downloading file {FileName} from project: {ProjectName} (processing: {IsProcessing})", 
                fileName, projectName, isProcessingFile);

            var userInfo = await context.GetUserInfoAsync();
            var (stream, contentType) = await projectService.DownloadFileAsync(projectName, fileName, isProcessingFile, cancellationToken);

            if (stream == null)
            {
                return Results.NotFound(new { message = $"File '{fileName}' not found in project '{projectName}'" });
            }

            var fileNameOnly = Path.GetFileName(fileName);
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
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
            
            if (extension == ".pdf")
            {
                context.Response.Headers["Content-Type"] = "application/pdf";
                context.Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileNameOnly}\"";
                context.Response.Headers["Accept-Ranges"] = "bytes";
                context.Response.Headers["Cache-Control"] = "public, max-age=3600";
                
                return Results.Stream(stream, contentType: "application/pdf", enableRangeProcessing: true);
            }
            
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
            logger.LogError(ex, "Error downloading file {FileName} from project: {ProjectName}", fileName, projectName);
            return Results.Problem("Error downloading file from project");
        }
    }
}
