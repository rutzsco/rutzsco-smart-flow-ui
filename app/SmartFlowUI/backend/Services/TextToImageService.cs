// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;
using System.Net.Http.Headers; // Required for MediaTypeHeaderValue
using System.Text;
using Azure;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Images;
using UglyToad.PdfPig.Content;
using System; // Added for ArgumentException
using System.IO; // Required for StreamContent if used, or for reading stream
using System.Threading; // Required for CancellationToken
using System.Collections.Generic; // Required for Dictionary

namespace MinimalApi.Services;

public class TextToImageService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TextToImageService(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(2); // Set a 2-minute timeout for all requests
        _configuration = configuration;
    }

    public async Task<string> EditImageFromDataUrlAsync(string imageBlobUrl, string prompt, string size = "1024x1024", int n = 1, string quality = "medium")
    {
        var endpoint = _configuration["TextToImageAPIEndpoint"];
        var deployment = _configuration["TextToImageAPIDeployment"];
        var apiKey = _configuration["TextToImageAPIKey"];
        var apiVersion = "2025-04-01-preview"; // Ensure this is the correct or latest stable API version.

        // Construct the base path and URL parameters as per the new structure.
        var basePath = $"/openai/deployments/{deployment}/images"; // Adjusted to match existing logic if 'deployment' is part of the path.
        var urlParams = $"?api-version={apiVersion}";
        var editUrl = $"{endpoint}{basePath}/edits{urlParams}";

        byte[] imageData;
        string imageMediaType = "image/png"; // Default media type
        try
        {
            // Download the image data from the provided URL
            using (var imageResponse = await _httpClient.GetAsync(imageBlobUrl))
            {
                imageResponse.EnsureSuccessStatusCode(); // Throw if not a success code.
                imageData = await imageResponse.Content.ReadAsByteArrayAsync();
                var contentTypeHeader = imageResponse.Content.Headers.ContentType;
                if (contentTypeHeader != null && !string.IsNullOrEmpty(contentTypeHeader.MediaType))
                {
                    imageMediaType = contentTypeHeader.MediaType; // Use actual media type if available
                }
            }
        }
        catch (HttpRequestException ex)
        {
            // Log or handle specific exception for image download failure
            throw new HttpRequestException($"Failed to download image from URL: {imageBlobUrl}", ex);
        }
        catch (TaskCanceledException ex) // Catches timeouts specifically for the image download
        {
            if (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested && _httpClient.Timeout < TimeSpan.MaxValue) // Check if timeout was the cause
            {
                throw new TimeoutException($"The request to download image from URL: {imageBlobUrl} timed out.", ex);
            }
            throw; // Re-throw if it's a different cancellation reason
        }

        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data could not be downloaded or is empty.", nameof(imageBlobUrl));
        }

        // Prepare the form data content
        using (var form = new MultipartFormDataContent())
        {
            // Use a dictionary for form fields as in the reference
            var editFields = new Dictionary<string, string>
            {
                { "prompt", prompt },
                { "n", n.ToString() },
                { "size", size },
                { "quality", quality }
            };

            foreach (var kvp in editFields)
            {
                form.Add(new StringContent(kvp.Value), kvp.Key);
            }

            // Add image content
            var imageContent = new ByteArrayContent(imageData);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(imageMediaType);
            string fileName = $"image.{imageMediaType.Split('/')[1]}"; // e.g., image.png, image.jpeg
            form.Add(imageContent, "image", fileName);

            // Prepare the HTTP request
            using (var request = new HttpRequestMessage(HttpMethod.Post, editUrl))
            {
                // Add the API key to the request headers
                request.Headers.Add("api-key", apiKey);
                request.Content = form;

                HttpResponseMessage response;
                try
                {
                    // Send the request. The HttpClient.Timeout set in the constructor will apply.
                    response = await _httpClient.SendAsync(request);
                }
                catch (TaskCanceledException ex) // Catches timeouts for the edit request
                {
                    // Check if the cancellation was due to a timeout
                    if (ex.InnerException is TimeoutException || (_httpClient.Timeout != System.Threading.Timeout.InfiniteTimeSpan && ex.CancellationToken.IsCancellationRequested))
                    {
                        throw new TimeoutException($"The image edit request to {editUrl} timed out.", ex);
                    }
                    throw; // Re-throw if it's a different cancellation reason
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Image edit API request failed with status code {response.StatusCode}: {jsonResponse}");
                }
                
                var responseObject = JObject.Parse(jsonResponse);

                var b64Json = responseObject?["data"]?[0]?["b64_json"]?.ToString();

                if (string.IsNullOrEmpty(b64Json))
                {
                    throw new HttpRequestException("Failed to retrieve base64 image data from API response for image edit. The response might not contain the expected data.");
                }

                return $"data:image/png;base64,{b64Json}"; // Assuming the edited image is always PNG. Adjust if the API can return other formats.
            }
        }
    }
}
