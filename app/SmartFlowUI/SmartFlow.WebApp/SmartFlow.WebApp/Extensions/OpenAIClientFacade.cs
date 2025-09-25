
using Azure.Core;
using Azure;
using Assistants.Hub.API.Assistants.RAG;

namespace MinimalApi.Extensions;

public class OpenAIClientFacade
{
    private readonly AppConfiguration _config;
    private readonly string _standardChatGptDeployment;
    private readonly string _standardServiceEndpoint;
    private readonly TokenCredential _tokenCredential;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SearchClientFactory _searchClientFactory;
    private readonly AzureKeyCredential _azureKeyCredential;

    private readonly AzureOpenAIClient _standardChatGptClient;

    public OpenAIClientFacade(AppConfiguration configuration, AzureKeyCredential azureKeyCredential, TokenCredential tokenCredential, IHttpClientFactory httpClientFactory, SearchClientFactory searchClientFactory)
    {
        ArgumentNullException.ThrowIfNull(configuration, "AppConfiguration");
        ArgumentNullException.ThrowIfNull(configuration.AOAIStandardChatGptDeployment, "AOAIStandardChatGptDeployment");
        ArgumentNullException.ThrowIfNull(configuration.AOAIStandardServiceEndpoint, "AOAIStandardServiceEndpoint");

        _config = configuration;
        _standardChatGptDeployment = _config.AOAIStandardChatGptDeployment;
        _standardServiceEndpoint = _config.AOAIStandardServiceEndpoint;

        _azureKeyCredential = azureKeyCredential;
        _tokenCredential = tokenCredential;
        _httpClientFactory = httpClientFactory;
        _searchClientFactory = searchClientFactory;

        if (azureKeyCredential != null)
            _standardChatGptClient = new AzureOpenAIClient(new Uri(_standardServiceEndpoint), _azureKeyCredential);
        else
            _standardChatGptClient = new AzureOpenAIClient(new Uri(_standardServiceEndpoint), _tokenCredential);
    }

    public string GetKernelDeploymentName()
    {
        return _standardChatGptDeployment;
    }

    public Kernel BuildKernel(string toolPackage)
    {
        var kernel = BuildKernelBasedOnIdentity();
        if (toolPackage == "RAG")
        {
            kernel.ImportPluginFromObject(new RAGRetrivalPlugins(_searchClientFactory, _standardChatGptClient), "RAGChat");
        }

        if(toolPackage == "ImageGen")
        {
            kernel = BuildImageGenerationKernelBasedOnIdentity();
        }
        return kernel;
    }

    private Kernel BuildKernelBasedOnIdentity()
    {
        if (_azureKeyCredential != null)
        {
            var keyKernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(_standardChatGptDeployment, _standardServiceEndpoint, _config.AOAIStandardServiceKey)
                .Build();
            return keyKernel;
        }

        var kernel = Kernel.CreateBuilder()
       .AddAzureOpenAIChatCompletion(_standardChatGptDeployment, _standardServiceEndpoint, _tokenCredential)
       .Build();

        return kernel;
    }

    #pragma warning disable SKEXP0010
    private Kernel BuildImageGenerationKernelBasedOnIdentity()
    {
        if (_azureKeyCredential != null)
        {

            var keyKernel = Kernel.CreateBuilder()
                .AddAzureOpenAITextToImage(_standardChatGptDeployment, _standardServiceEndpoint,_config.AOAIStandardServiceKey)
                .Build();

            return keyKernel;
        }

        var kernel = Kernel.CreateBuilder()
            .AddAzureOpenAITextToImage("dall-e-3", _standardServiceEndpoint, _tokenCredential)
            .Build();

        return kernel;
    }
    #pragma warning restore SKEXP0010
}
