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
using System;
using System.IO; 
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MinimalApi.Services;

public class TextToImageService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TextToImageService(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(2); 
        _configuration = configuration;
    }

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

    public async Task<string> EditImageFromDataUrlAsync(string imageSourceUrl, string prompt, string size = "1024x1024", int n = 1, string quality = "medium")
    {
        var endpoint = _configuration["TextToImageAPIEndpoint"];
        var deployment = _configuration["TextToImageAPIDeployment"];
        var apiKey = _configuration["TextToImageAPIKey"];
        var apiVersion = "2025-04-01-preview";

        var basePath = $"/openai/deployments/{deployment}/images";
        var urlParams = $"?api-version={apiVersion}";
        var editUrl = $"{endpoint}{basePath}/edits{urlParams}";

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
            try
            {
                using (var imageResponse = await _httpClient.GetAsync(imageSourceUrl))
                {
                    imageResponse.EnsureSuccessStatusCode();
                    imageData = await imageResponse.Content.ReadAsByteArrayAsync();
                    var contentTypeHeader = imageResponse.Content.Headers.ContentType;
                    if (contentTypeHeader != null && !string.IsNullOrEmpty(contentTypeHeader.MediaType))
                    {
                        imageMediaType = contentTypeHeader.MediaType;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"Failed to download image from URL: {imageSourceUrl}", ex);
            }
            catch (TaskCanceledException ex)
            {
                if (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested && _httpClient.Timeout < TimeSpan.MaxValue)
                {
                    throw new TimeoutException($"The request to download image from URL: {imageSourceUrl} timed out.", ex);
                }
                throw;
            }
        }

        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data could not be processed or is empty.", nameof(imageSourceUrl));
        }

        using (var form = new MultipartFormDataContent())
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

            var imageContent = new ByteArrayContent(imageData);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(imageMediaType);
            string fileName = $"image.{imageMediaType.Split('/')[1]}";
            form.Add(imageContent, "image", fileName);

            using (var request = new HttpRequestMessage(HttpMethod.Post, editUrl))
            {
                request.Headers.Add("api-key", apiKey);
                request.Content = form;

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request);
                }
                catch (TaskCanceledException ex)
                {
                    if (ex.InnerException is TimeoutException || (_httpClient.Timeout != System.Threading.Timeout.InfiniteTimeSpan && ex.CancellationToken.IsCancellationRequested))
                    {
                        throw new TimeoutException($"The image edit request to {editUrl} timed out.", ex);
                    }
                    throw;
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

                return $"data:image/png;base64,{b64Json}";
            }
        }
    }

    public async Task<string> NewImageAsync(string prompt, string size = "1024x1024", int n = 1, string quality = "medium", string outputFormat = "png")
    {
        var endpoint = _configuration["TextToImageAPIEndpoint"];
        var deployment = _configuration["TextToImageAPIDeployment"];
        var apiKey = _configuration["TextToImageAPIKey"];
        var apiVersion = "2025-04-01-preview";

        var basePath = $"/openai/deployments/{deployment}/images"; 
        var urlParams = $"?api-version={apiVersion}";
        var generationUrl = $"{endpoint}{basePath}/generations{urlParams}";


        var requestBody = new
        {
            prompt = prompt,
            n = n,
            size = size,
            quality = quality,
            output_format = outputFormat,
        };
        using (var request = new HttpRequestMessage(HttpMethod.Post, generationUrl))
        {
            request.Headers.Add("api-key", apiKey);
            var jsonRequestBody = JsonConvert.SerializeObject(requestBody);
            request.Content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request);
            }
            catch (TaskCanceledException ex)
            {
                if (ex.InnerException is TimeoutException || (_httpClient.Timeout != System.Threading.Timeout.InfiniteTimeSpan && ex.CancellationToken.IsCancellationRequested))
                {
                    throw new TimeoutException($"The image generation request to {generationUrl} timed out.", ex);
                }
                throw;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Image generation API request failed with status code {response.StatusCode}: {jsonResponse}");
            }

            var responseObject = JObject.Parse(jsonResponse);
            var b64Json = responseObject?["data"]?[0]?["b64_json"]?.ToString();

            if (string.IsNullOrEmpty(b64Json))
            {
                throw new HttpRequestException("Failed to retrieve base64 image data from API response for image generation. The response might not contain the expected data.");
            }

            // Determine the correct media type based on the output format if it's not b64_json
            // For now, assuming b64_json implies PNG. If other formats are supported via b64_json, this might need adjustment.
            string responseMediaType = "image/png"; 
            return $"data:{responseMediaType};base64,{b64Json}";
        }
    }
}
