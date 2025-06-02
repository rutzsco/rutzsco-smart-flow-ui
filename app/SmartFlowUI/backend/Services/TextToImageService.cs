// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Azure;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Images;
using UglyToad.PdfPig.Content;

namespace MinimalApi.Services;

public class TextToImageService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TextToImageService(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _configuration = configuration;
    }

    public async Task EditImageFromDataUrlAsync(string imageDataUrl, string prompt, string size = "1024x1024", int n = 1, string quality = "medium")
    {
        var endpoint = _configuration["TextToImageAPIEndpoint"];
        var deployment = _configuration["TextToImageAPIDeployment"];
        var apiKey = _configuration["TextToImageAPIKey"];
        var apiVersion = "2024-02-15-preview";

        var basePath = $"openai/deployments/{deployment}/images";
        var parameters = $"?api-version={apiVersion}";
        var generationUrl = $"{endpoint}{basePath}/generations{parameters}";

        var generationBody = new
        {
            prompt,
            n,
            size,
            quality,
            output_format = "png" // Assuming png is the desired output, adjust if necessary
        };

        var request = new HttpRequestMessage(HttpMethod.Post, generationUrl)
        {
            Content = new StringContent(JsonConvert.SerializeObject(generationBody), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("Api-Key", apiKey);

        var response = await _httpClient.SendAsync(request);
    }
}
