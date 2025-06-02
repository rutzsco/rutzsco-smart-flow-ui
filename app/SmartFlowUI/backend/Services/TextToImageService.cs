// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MinimalApi.Services;

/// <summary>
/// Service for generating and editing images using Azure OpenAI APIs
/// </summary>
public class TextToImageService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    
    // API configuration properties
    private string ApiEndpoint => _configuration["TextToImageAPIEndpoint"];
    private string ApiDeployment => _configuration["TextToImageAPIDeployment"];
    private string ApiKey => _configuration["TextToImageAPIKey"];
    private string ApiVersion => "2025-04-01-preview";
    private string BaseApiPath => $"/openai/deployments/{ApiDeployment}/images";

    /// <summary>
    /// Initializes a new instance of the TextToImageService
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    public TextToImageService(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(2); 
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Generates a new image based on the provided text prompt
    /// </summary>
    /// <param name="prompt">Text description of the desired image</param>
    /// <param name="size">Image size (e.g., "1024x1024")</param>
    /// <param name="n">Number of images to generate</param>
    /// <param name="quality">Image quality (e.g., "medium")</param>
    /// <param name="outputFormat">Output format (default: "png")</param>
    /// <returns>Base64 data URL of the generated image</returns>
    public async Task<string> NewImageAsync(
        string prompt, 
        string size = "1024x1024", 
        int n = 1, 
        string quality = "medium", 
        string outputFormat = "png")
    {
        var requestUrl = $"{ApiEndpoint}{BaseApiPath}/generations?api-version={ApiVersion}";
        
        var requestBody = new ImageGenerationRequest
        {
            Prompt = prompt,
            N = n,
            Size = size,
            Quality = quality,
            OutputFormat = outputFormat
        };
        
        var jsonResponse = await SendApiRequestAsync(
            HttpMethod.Post, 
            requestUrl, 
            JsonConvert.SerializeObject(requestBody),
            "application/json");
        
        return ExtractImageDataUrl(jsonResponse, "image generation");
    }

    /// <summary>
    /// Edits an existing image based on a text prompt
    /// </summary>
    /// <param name="imageSourceUrl">URL or data URL of the image to edit</param>
    /// <param name="prompt">Text description of the desired edits</param>
    /// <param name="size">Image size (e.g., "1024x1024")</param>
    /// <param name="n">Number of edited images to generate</param>
    /// <param name="quality">Image quality (e.g., "medium")</param>
    /// <returns>Base64 data URL of the edited image</returns>
    public async Task<string> EditImageFromDataUrlAsync(
        string imageSourceUrl, 
        string prompt, 
        string size = "1024x1024", 
        int n = 1, 
        string quality = "medium")
    {
        var requestUrl = $"{ApiEndpoint}{BaseApiPath}/edits?api-version={ApiVersion}";
        
        // Extract image data from URL or data URL
        var (imageData, imageMediaType) = await GetImageDataAsync(imageSourceUrl);
        
        // Prepare form data for the edit request
        using var form = new MultipartFormDataContent();
        AddEditRequestFields(form, prompt, n, size, quality);
        AddImageContent(form, imageData, imageMediaType);
        
        var jsonResponse = await SendApiRequestAsync(HttpMethod.Post, requestUrl, form);
        
        return ExtractImageDataUrl(jsonResponse, "image edit");
    }

    #region Private Helper Methods
    
    /// <summary>
    /// Parses a base64 data URL to extract binary data and media type
    /// </summary>
    private (byte[] Data, string MediaType) ParseDataUrl(string dataUrl)
    {
        var match = Regex.Match(dataUrl, @"^data:(?<type>image\/\w+);base64,(?<data>.+)$");
        if (!match.Success)
        {
            throw new ArgumentException("Invalid data URL format.", nameof(dataUrl));
        }

        var mediaType = match.Groups["type"].Value;
        var base64Data = match.Groups["data"].Value;
        var data = Convert.FromBase64String(base64Data);
        return (data, mediaType);
    }
    
    /// <summary>
    /// Gets image data from either a URL or data URL
    /// </summary>
    private async Task<(byte[] Data, string MediaType)> GetImageDataAsync(string imageSourceUrl)
    {
        if (string.IsNullOrEmpty(imageSourceUrl))
        {
            throw new ArgumentNullException(nameof(imageSourceUrl), "Image source URL cannot be null or empty.");
        }
        
        byte[] imageData;
        string imageMediaType = "image/png";

        if (imageSourceUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                (imageData, imageMediaType) = ParseDataUrl(imageSourceUrl);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Failed to parse data URL: {imageSourceUrl}", ex);
            }
        }
        else
        {
            (imageData, imageMediaType) = await DownloadImageFromUrlAsync(imageSourceUrl);
        }

        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data could not be processed or is empty.", nameof(imageSourceUrl));
        }
        
        return (imageData, imageMediaType);
    }
    
    /// <summary>
    /// Downloads an image from a URL and returns its data and media type
    /// </summary>
    private async Task<(byte[] Data, string MediaType)> DownloadImageFromUrlAsync(string imageUrl)
    {
        string mediaType = "image/png";
        try
        {
            using var imageResponse = await _httpClient.GetAsync(imageUrl);
            imageResponse.EnsureSuccessStatusCode();
            
            var contentTypeHeader = imageResponse.Content.Headers.ContentType;
            if (contentTypeHeader != null && !string.IsNullOrEmpty(contentTypeHeader.MediaType))
            {
                mediaType = contentTypeHeader.MediaType;
            }
            
            var data = await imageResponse.Content.ReadAsByteArrayAsync();
            return (data, mediaType);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"Failed to download image from URL: {imageUrl}", ex);
        }
        catch (TaskCanceledException ex)
        {
            if (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested && _httpClient.Timeout < TimeSpan.MaxValue)
            {
                throw new TimeoutException($"The request to download image from URL: {imageUrl} timed out.", ex);
            }
            throw;
        }
    }
    
    /// <summary>
    /// Adds fields to a multipart form for image edit requests
    /// </summary>
    private void AddEditRequestFields(MultipartFormDataContent form, string prompt, int n, string size, string quality)
    {
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
    }
    
    /// <summary>
    /// Adds image content to a multipart form
    /// </summary>
    private void AddImageContent(MultipartFormDataContent form, byte[] imageData, string imageMediaType)
    {
        var imageContent = new ByteArrayContent(imageData);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(imageMediaType);
        string fileName = $"image.{imageMediaType.Split('/')[1]}";
        form.Add(imageContent, "image", fileName);
    }
    
    /// <summary>
    /// Sends an API request with JSON content
    /// </summary>
    private async Task<string> SendApiRequestAsync(HttpMethod method, string url, string jsonContent, string contentType)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("api-key", ApiKey);
        request.Content = new StringContent(jsonContent, Encoding.UTF8, contentType);
        
        return await ExecuteApiRequestAsync(request, url);
    }
    
    /// <summary>
    /// Sends an API request with form content
    /// </summary>
    private async Task<string> SendApiRequestAsync(HttpMethod method, string url, MultipartFormDataContent content)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("api-key", ApiKey);
        request.Content = content;
        
        return await ExecuteApiRequestAsync(request, url);
    }
    
    /// <summary>
    /// Executes an API request and handles errors
    /// </summary>
    private async Task<string> ExecuteApiRequestAsync(HttpRequestMessage request, string url)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request);
        }
        catch (TaskCanceledException ex)
        {
            if (ex.InnerException is TimeoutException || 
                (_httpClient.Timeout != Timeout.InfiniteTimeSpan && ex.CancellationToken.IsCancellationRequested))
            {
                throw new TimeoutException($"The API request to {url} timed out.", ex);
            }
            throw;
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API request failed with status code {response.StatusCode}: {jsonResponse}");
        }
        
        return jsonResponse;
    }
    
    /// <summary>
    /// Extracts the base64 image data from an API response
    /// </summary>
    private string ExtractImageDataUrl(string jsonResponse, string operationName)
    {
        var responseObject = JObject.Parse(jsonResponse);
        var b64Json = responseObject?["data"]?[0]?["b64_json"]?.ToString();

        if (string.IsNullOrEmpty(b64Json))
        {
            throw new HttpRequestException($"Failed to retrieve base64 image data from API response for {operationName}. The response might not contain the expected data.");
        }

        return $"data:image/png;base64,{b64Json}";
    }
    
    #endregion
    
    #region Models
    
    /// <summary>
    /// Request model for image generation
    /// </summary>
    private class ImageGenerationRequest
    {
        [JsonProperty("prompt")]
        public string Prompt { get; set; }
        
        [JsonProperty("n")]
        public int N { get; set; }
        
        [JsonProperty("size")]
        public string Size { get; set; }
        
        [JsonProperty("quality")]
        public string Quality { get; set; }
        
        [JsonProperty("output_format")]
        public string OutputFormat { get; set; }
    }
    
    #endregion
}
