// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Options;

namespace MinimalApi.Services;

public sealed class AzureBlobStorageService(BlobServiceClient blobServiceClient, IOptions<AppConfiguration> configuration, ProfileService profileService)
{
    internal async Task<UploadDocumentsResponse> UploadFilesAsync(UserInformation userInfo, IEnumerable<IFormFile> files, string storageContainerName, IDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        try
        {
            var container = blobServiceClient.GetBlobContainerClient(storageContainerName);
            if (!await container.ExistsAsync())
            {
                // Create the container
                await container.CreateAsync();
                Console.WriteLine("Container created.");
            }

            List<UploadDocumentFileSummary> uploadedFiles = [];
            foreach (var file in files)
            {
                var fileName = file.FileName;

                await using var stream = file.OpenReadStream();
                // Use the original filename without timestamp
                var blobName = Path.GetFileName(fileName);
                var blobClient = container.GetBlobClient(blobName);
                //if (await blobClient.ExistsAsync(cancellationToken))
                //{
                //    continue;
                //}

                var url = blobClient.Uri.AbsoluteUri;
                await using var fileStream = file.OpenReadStream();
                await blobClient.UploadAsync(fileStream, new BlobHttpHeaders
                {
                    ContentType = "image"
                }, metadata, cancellationToken: cancellationToken);

                var companyName = metadata.TryGetValue("CompanyName", out string? companyNameValue) ? companyNameValue : string.Empty;
                var industry = metadata.TryGetValue("Industry", out string? industryValue) ? industryValue : string.Empty;

                uploadedFiles.Add(new UploadDocumentFileSummary(blobName, file.Length, companyName, industry));
            }

            if (uploadedFiles.Count is 0)
            {
                return UploadDocumentsResponse.FromError("No files were uploaded. Either the files already exist or the files are not PDFs or images.");
            }

            return new UploadDocumentsResponse([.. uploadedFiles]);
        }
        catch (Exception ex)
        {
            return UploadDocumentsResponse.FromError(ex.ToString());
        }
    }

    private static string BlobNameFromFilePage(string filename, long page = 0)
    {
        return Path.GetExtension(filename).ToLower() is ".pdf"
            ? $"{Path.GetFileNameWithoutExtension(filename)}_{page}.pdf"
            : Path.GetFileName(filename);
    }
}
