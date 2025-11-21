using Azure;
using Azure.Core;
using Assistants.Hub.API.Assistants.RAG;
using Azure.Core.Pipeline;
using System.ClientModel.Primitives;

namespace MinimalApi.Extensions;

public class OpenAIClientFacade
{
    private readonly AppConfiguration _config;
    private readonly string _standardChatGptDeployment;
    private readonly string? _embeddingsDeploymentKey;
    private readonly string _standardServiceEndpoint;
    private readonly TokenCredential _tokenCredential;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SearchClientFactory _searchClientFactory;
    private readonly AzureKeyCredential _azureKeyCredential;
    private readonly string _apimKey;

    private readonly AzureOpenAIClient _standardChatGptClient;
    private readonly AzureOpenAIClient _standardEmbeddingsClient;

    public OpenAIClientFacade(AppConfiguration configuration, AzureKeyCredential azureKeyCredential, TokenCredential tokenCredential, IHttpClientFactory httpClientFactory, SearchClientFactory searchClientFactory, string apimKey = null)
    {
        ArgumentNullException.ThrowIfNull(configuration, "AppConfiguration");
        ArgumentNullException.ThrowIfNull(configuration.AOAIStandardChatGptDeployment, "AOAIStandardChatGptDeployment");
        ArgumentNullException.ThrowIfNull(configuration.AOAIStandardServiceEndpoint, "AOAIStandardServiceEndpoint");

        _config = configuration;
        _standardChatGptDeployment = _config.AOAIStandardChatGptDeployment;
        _embeddingsDeploymentKey = _config.AOAIEmbeddingsDeploymentKey;
        _standardServiceEndpoint = _config.AOAIStandardServiceEndpoint;

        _azureKeyCredential = azureKeyCredential;
        _tokenCredential = tokenCredential;
        _httpClientFactory = httpClientFactory;
        _searchClientFactory = searchClientFactory;
        _apimKey = apimKey;

        // Create chat client with APIM key header if provided
        if (!string.IsNullOrEmpty(_apimKey))
        {
            // HttpClient from IHttpClientFactory is managed by the factory - no disposal needed
#pragma warning disable CA2000 // Dispose objects before losing scope
            var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apimKey);

            var clientOptions = new AzureOpenAIClientOptions
            {
                Transport = new HttpClientPipelineTransport(httpClient)
            };

            if (azureKeyCredential != null)
                _standardChatGptClient = new AzureOpenAIClient(new Uri(_standardServiceEndpoint), _azureKeyCredential, clientOptions);
            else
                _standardChatGptClient = new AzureOpenAIClient(new Uri(_standardServiceEndpoint), _tokenCredential, clientOptions);
        }
        else
        {
            if (azureKeyCredential != null)
                _standardChatGptClient = new AzureOpenAIClient(new Uri(_standardServiceEndpoint), _azureKeyCredential);
            else
                _standardChatGptClient = new AzureOpenAIClient(new Uri(_standardServiceEndpoint), _tokenCredential);
        }

        // Create embeddings client if key is configured
        if (!string.IsNullOrEmpty(_embeddingsDeploymentKey))
        {
            var embeddingsKeyCredential = new AzureKeyCredential(_embeddingsDeploymentKey);

            if (!string.IsNullOrEmpty(_embeddingsDeploymentKey))
            {
                // HttpClient from IHttpClientFactory is managed by the factory - no disposal needed
#pragma warning disable CA2000 // Dispose objects before losing scope
                var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope
                httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _embeddingsDeploymentKey);

                var clientOptions = new AzureOpenAIClientOptions
                {
                    Transport = new HttpClientPipelineTransport(httpClient)
                };

                _standardEmbeddingsClient = new AzureOpenAIClient(new Uri(_standardServiceEndpoint), embeddingsKeyCredential, clientOptions);
            }
            else
            {
                _standardEmbeddingsClient = new AzureOpenAIClient(new Uri(_standardServiceEndpoint), embeddingsKeyCredential);
            }
        }
    }

    public string GetKernelDeploymentName()
    {
        return _standardChatGptDeployment;
    }

    public AzureOpenAIClient GetChatClient()
    {
        return _standardChatGptClient;
    }

    public AzureOpenAIClient GetEmbeddingsClient()
    {
        return _standardEmbeddingsClient ?? _standardChatGptClient;
    }

    public Kernel BuildKernel(string toolPackage)
    {
        var kernel = BuildKernelBasedOnIdentity();
        if (toolPackage == "RAG")
        {
            kernel.ImportPluginFromObject(new RAGRetrivalPlugins(_searchClientFactory, GetEmbeddingsClient()), "RAGChat");
        }

        if(toolPackage == "ImageGen")
        {
            kernel = BuildImageGenerationKernelBasedOnIdentity();
        }
        return kernel;
    }

    private Kernel BuildKernelBasedOnIdentity()
    {
        var kernelBuilder = Kernel.CreateBuilder();

        if (_azureKeyCredential != null)
        {
            // Use key-based authentication
            if (!string.IsNullOrEmpty(_apimKey))
            {
                // HttpClient from IHttpClientFactory is managed by the factory - no disposal needed
                #pragma warning disable CA2000 // Dispose objects before losing scope
                var httpClient = _httpClientFactory.CreateClient();
                #pragma warning restore CA2000 // Dispose objects before losing scope
                httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apimKey);

                kernelBuilder.AddAzureOpenAIChatCompletion(
                    _standardChatGptDeployment,
                    _standardServiceEndpoint,
                    _config.AOAIStandardServiceKey,
                    httpClient: httpClient);
            }
            else
            {
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    _standardChatGptDeployment,
                    _standardServiceEndpoint,
                    _config.AOAIStandardServiceKey);
            }
        }
        else
        {
            // Use token-based authentication
            if (!string.IsNullOrEmpty(_apimKey))
            {
                // Create HttpClient with APIM key header - managed by IHttpClientFactory
#pragma warning disable CA2000 // Dispose objects before losing scope
                var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope
                httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apimKey);

                kernelBuilder.AddAzureOpenAIChatCompletion(
                    _standardChatGptDeployment,
                    _standardServiceEndpoint,
                    _tokenCredential,
                    httpClient: httpClient);
            }
            else
            {
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    _standardChatGptDeployment,
                    _standardServiceEndpoint,
                    _tokenCredential);
            }
        }

        return kernelBuilder.Build();
    }

    #pragma warning disable SKEXP0010
    private Kernel BuildImageGenerationKernelBasedOnIdentity()
    {
        var kernelBuilder = Kernel.CreateBuilder();

        if (_azureKeyCredential != null)
        {
            // Use key-based authentication
            if (!string.IsNullOrEmpty(_apimKey))
            {
                // Create HttpClient with APIM key header - managed by IHttpClientFactory
#pragma warning disable CA2000 // Dispose objects before losing scope
                var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope
                httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apimKey);

                kernelBuilder.AddAzureOpenAITextToImage(
                    _standardChatGptDeployment,
                    _standardServiceEndpoint,
                    _config.AOAIStandardServiceKey,
                    httpClient: httpClient);
            }
            else
            {
                kernelBuilder.AddAzureOpenAITextToImage(
                    _standardChatGptDeployment,
                    _standardServiceEndpoint,
                    _config.AOAIStandardServiceKey);
            }
        }
        else
        {
            // Use token-based authentication
            if (!string.IsNullOrEmpty(_apimKey))
            {
                // Create HttpClient with APIM key header - managed by IHttpClientFactory
#pragma warning disable CA2000 // Dispose objects before losing scope
                var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope
                httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apimKey);

                kernelBuilder.AddAzureOpenAITextToImage(
                    "dall-e-3",
                    _standardServiceEndpoint,
                    _tokenCredential,
                    httpClient: httpClient);
            }
            else
            {
                kernelBuilder.AddAzureOpenAITextToImage(
                    "dall-e-3",
                    _standardServiceEndpoint,
                    _tokenCredential);
            }
        }

        return kernelBuilder.Build();
    }
    #pragma warning restore SKEXP0010
}
