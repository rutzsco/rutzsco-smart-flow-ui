// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MinimalApi.Services.HealthChecks;

public class AzureStorageReadinessHealthCheck(BlobServiceClient blobServiceClient, AppConfiguration configuration) : IHealthCheck
{
    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private readonly AppConfiguration _configuration = configuration;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        Dictionary<string, object> data = [];
        var containerName = _configuration.UserDocumentUploadBlobStorageContentContainer;

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            await _blobServiceClient.GetBlobContainersAsync(cancellationToken: cancellationToken).AsPages(pageSizeHint: 1).GetAsyncEnumerator(cancellationToken).MoveNextAsync().ConfigureAwait(false);

            data.Add("Storage Account", $"Storage Account is accessible: {_blobServiceClient.AccountName}");
        }
        catch (Exception ex)
        {
            data.Add("Storage Account", $"Storage Account is not accessible: {_blobServiceClient.AccountName}");
            return new HealthCheckResult(HealthStatus.Unhealthy, description: "Storage Account is not accessible", exception: ex, data: data);
        }
#pragma warning restore CA1031 // Do not catch general exception types

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_configuration.UserDocumentUploadBlobStorageContentContainer);
            await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            data.Add("Container", $"Container is accessible: {_configuration.UserDocumentUploadBlobStorageContentContainer}");
        }
        catch (Exception ex)
        {
            data.Add("Container", $"Container is accessible: {_configuration.UserDocumentUploadBlobStorageContentContainer}");
            return new HealthCheckResult(HealthStatus.Unhealthy, description: "Storage Account is not accessible", exception: ex, data: data);
        }
#pragma warning restore CA1031 // Do not catch general exception types

        return new HealthCheckResult(HealthStatus.Healthy, description: "Storage Account is accessible", data: data);
    }
}
