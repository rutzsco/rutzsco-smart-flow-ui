
// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.Storage;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MinimalApi.Services.ChatHistory;
using MinimalApi.Services.HealthChecks;
using MinimalApi.Services.Documents;
using MinimalApi.Services.Search;
using MinimalApi.Services.Skills;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.TextGeneration;
using System.ClientModel.Primitives;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Azure;

namespace MinimalApi.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddAzureServices(this IServiceCollection services, AppConfiguration configuration)
    {
        var sp = services.BuildServiceProvider();
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();

        services.AddAzureClients(builder =>
        {
            if (!string.IsNullOrEmpty(configuration.VisualStudioTenantId))
            {
                builder.UseCredential(new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    VisualStudioTenantId = configuration.VisualStudioTenantId
                }));
            }
            else
            {
                if (!string.IsNullOrEmpty(configuration.UserAssignedManagedIdentityClientId))
                {
                    builder.UseCredential(new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        ManagedIdentityClientId = configuration.UserAssignedManagedIdentityClientId
                    }));
                }
                else
                {
                    builder.UseCredential(new DefaultAzureCredential(new DefaultAzureCredentialOptions()));
                }
            }

            builder.AddBlobServiceClient(configuration.AzureStorageAccountEndpoint);
        });

        services.AddSingleton<BlobContainerClient>(sp =>
        {
            var azureStorageContainer = configuration.AzureStorageContainer;
            return sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(azureStorageContainer);
        });

        services.AddSingleton<OpenAIClientFacade>(sp =>
        {
            var standardChatGptDeployment = configuration.AOAIStandardChatGptDeployment;
            var standardServiceEndpoint = configuration.AOAIStandardServiceEndpoint;
            var standardServiceKey = configuration.AOAIStandardServiceKey;

            ArgumentNullException.ThrowIfNullOrEmpty(standardChatGptDeployment);
            ArgumentNullException.ThrowIfNullOrEmpty(standardServiceEndpoint);

            var premiumChatGptDeployment = configuration.AOAIPremiumChatGptDeployment;
            var premiumServiceEndpoint = configuration.AOAIPremiumServiceEndpoint;
            var premiumServiceKey = configuration.AOAIPremiumServiceKey;

            // Build Plugins
            var searchClientFactory = sp.GetRequiredService<SearchClientFactory>();

            AzureOpenAIClient? openAIClient3 = null;
            AzureOpenAIClient? openAIClient4 = null;

            if (UseAPIMAIGatewayOBO(configuration))
            {
                SetupOpenAIClientsUsingOnBehalfOfOthersFlowAndSubscriptionKey(sp, httpContextAccessor, configuration, standardServiceEndpoint, out openAIClient3, out openAIClient4);
            }
            else
            {
                ArgumentNullException.ThrowIfNullOrEmpty(standardServiceKey);

                openAIClient3 = new AzureOpenAIClient(new Uri(standardServiceEndpoint), new AzureKeyCredential(standardServiceKey));
                if (!string.IsNullOrEmpty(premiumServiceEndpoint))
                    openAIClient4 = new AzureOpenAIClient(new Uri(premiumServiceEndpoint), new AzureKeyCredential(premiumServiceKey));
            }

            var retrieveRelatedDocumentPlugin3 = new RetrieveRelatedDocumentSkill(configuration, searchClientFactory, openAIClient3);
            var retrieveRelatedDocumentPluginKM = new RetrieveRelatedDocumentSkillKM(configuration, searchClientFactory, openAIClient3);
            var retrieveRelatedDocumentPlugin4 = new RetrieveRelatedDocumentSkill(configuration, searchClientFactory, openAIClient4);

            var generateSearchQueryPlugin = new GenerateSearchQuerySkill();
            var chatPlugin = new ChatSkill();

            Kernel? kernel3 = null;
            Kernel? kernel4 = null;
            IKernelBuilder? builder3 = null;
            IKernelBuilder? builder4 = null;

            if (openAIClient3 != null)
            {
#pragma warning disable IDE0039 // Use local function
                Func<IServiceProvider, object?, AzureOpenAIChatCompletionService> factory3 = (serviceProvider, _) => new(standardChatGptDeployment, openAIClient3, null, serviceProvider.GetService<ILoggerFactory>());
#pragma warning restore IDE0039 // Use local function

                var kernel3Builder = Kernel.CreateBuilder();
                kernel3Builder.Services.AddKeyedScoped<IChatCompletionService>(null, factory3);
                kernel3Builder.Services.AddKeyedScoped<ITextGenerationService>(null, factory3);

                kernel3 = kernel3Builder.Build();
            }
            else
            {
                // Build Kernels
                kernel3 = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(standardChatGptDeployment, standardServiceEndpoint, standardServiceKey)
                .Build();
            }

            if (openAIClient4 != null)
            {
#pragma warning disable IDE0039 // Use local function
                Func<IServiceProvider, object?, AzureOpenAIChatCompletionService> factory4 = (serviceProvider, _) => new(standardChatGptDeployment, openAIClient4, null, serviceProvider.GetService<ILoggerFactory>());
#pragma warning restore IDE0039 // Use local function

                var kernel4Builder = Kernel.CreateBuilder();
                kernel4Builder.Services.AddKeyedScoped<IChatCompletionService>(null, factory4);
                kernel4Builder.Services.AddKeyedScoped<ITextGenerationService>(null, factory4);

                kernel4 = kernel4Builder.Build();

            }
            //else
            //{
            //    kernel4 = Kernel.CreateBuilder()
            //    .AddAzureOpenAIChatCompletion(premiumChatGptDeployment, premiumServiceEndpoint, premiumServiceKey)
            //    .Build();
            //}
            kernel3.ImportPluginFromObject(retrieveRelatedDocumentPlugin3, DefaultSettings.DocumentRetrievalPluginName);
            kernel3.ImportPluginFromObject(retrieveRelatedDocumentPluginKM, DefaultSettings.DocumentRetrievalPluginNameKM);
            kernel3.ImportPluginFromObject(generateSearchQueryPlugin, DefaultSettings.GenerateSearchQueryPluginName);
            kernel3.ImportPluginFromObject(chatPlugin, DefaultSettings.ChatPluginName);

            if (kernel4 != null)
            {
                kernel4.ImportPluginFromObject(retrieveRelatedDocumentPlugin4, DefaultSettings.DocumentRetrievalPluginName);
                kernel4.ImportPluginFromObject(generateSearchQueryPlugin, DefaultSettings.GenerateSearchQueryPluginName);
                kernel4.ImportPluginFromObject(chatPlugin, DefaultSettings.ChatPluginName);
            }

            return new OpenAIClientFacade(standardChatGptDeployment, kernel3, premiumChatGptDeployment, kernel4);
        });

        services.AddSingleton<SearchClientFactory>(sp =>
        {
            return new SearchClientFactory(configuration, null, new AzureKeyCredential(configuration.AzureSearchServiceKey));
        });

        if (!string.IsNullOrEmpty(configuration.CosmosDBConnectionString))
        {
            services.AddSingleton((sp) =>
            {
                CosmosClientBuilder configurationBuilder = new CosmosClientBuilder(configuration.CosmosDBConnectionString);
                return configurationBuilder
                        .Build();
            });
        }

        RegisterDomainServices(services, configuration);

        return services;
    }

    internal static IServiceCollection AddAzureWithMICredentialsServices(this IServiceCollection services, AppConfiguration configuration)
    {
        var sp = services.BuildServiceProvider();
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        DefaultAzureCredential azureCredential;
        if (!string.IsNullOrEmpty(configuration.VisualStudioTenantId))
        {
            azureCredential = new(new DefaultAzureCredentialOptions { VisualStudioTenantId = configuration.VisualStudioTenantId });
        }
        else
        {
            if (!string.IsNullOrEmpty(configuration.UserAssignedManagedIdentityClientId))
            {
                azureCredential = new(new DefaultAzureCredentialOptions { ManagedIdentityClientId = configuration.UserAssignedManagedIdentityClientId });
            }
            else
            {
                azureCredential = new(new DefaultAzureCredentialOptions());
            }
        }
        services.AddSingleton<BlobServiceClient>(sp =>
        {
            var azureStorageAccountEndpoint = configuration.AzureStorageAccountEndpoint;
            ArgumentNullException.ThrowIfNullOrEmpty(azureStorageAccountEndpoint);

            var blobServiceClient = new BlobServiceClient(new Uri(azureStorageAccountEndpoint), azureCredential);

            return blobServiceClient;
        });

        services.AddSingleton<BlobContainerClient>(sp =>
        {
            var azureStorageContainer = configuration.AzureStorageContainer;
            return sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(azureStorageContainer);
        });

        services.AddSingleton(sp =>
        {
            var standardChatGptDeployment = configuration.AOAIStandardChatGptDeployment;
            var standardServiceEndpoint = configuration.AOAIStandardServiceEndpoint;

            ArgumentNullException.ThrowIfNullOrEmpty(standardChatGptDeployment);
            ArgumentNullException.ThrowIfNullOrEmpty(standardServiceEndpoint);

            var premiumChatGptDeployment = configuration.AOAIPremiumChatGptDeployment;
            var premiumServiceEndpoint = configuration.AOAIPremiumServiceEndpoint;

            AzureOpenAIClient? standardChatGptClient = null;
            AzureOpenAIClient? premiumChatGptClient = null;

            if (UseAPIMAIGatewayOBO(configuration))
            {
                SetupOpenAIClientsUsingOnBehalfOfOthersFlowAndSubscriptionKey(sp, httpContextAccessor, configuration, standardServiceEndpoint, out standardChatGptClient, out premiumChatGptClient);
            }
            else
            {
                standardChatGptClient = new AzureOpenAIClient(new Uri(standardServiceEndpoint), azureCredential);
                if (!string.IsNullOrEmpty(premiumChatGptDeployment))
                    premiumChatGptClient = new AzureOpenAIClient(new Uri(premiumServiceEndpoint), azureCredential);
            }

            // Build Plugins
            var searchClientFactory = sp.GetRequiredService<SearchClientFactory>();

            var retrieveRelatedDocumentPlugin3 = new RetrieveRelatedDocumentSkill(configuration, searchClientFactory, standardChatGptClient);
            var retrieveRelatedDocumentPlugin4 = new RetrieveRelatedDocumentSkill(configuration, searchClientFactory, premiumChatGptClient);

            var generateSearchQueryPlugin = new GenerateSearchQuerySkill();
            var chatPlugin = new ChatSkill();

            Kernel? kernelStandard = null;
            Kernel? kernelPremium = null;

            // Build Kernels
            if (UseAPIMAIGatewayOBO(configuration))
            {
#pragma warning disable IDE0039 // Use local function
                Func<IServiceProvider, object?, AzureOpenAIChatCompletionService> factory3 = (serviceProvider, _) => new(standardChatGptDeployment, standardChatGptClient, null, serviceProvider.GetService<ILoggerFactory>());
#pragma warning restore IDE0039 // Use local function

                var kernel3Builder = Kernel.CreateBuilder();
                kernel3Builder.Services.AddKeyedScoped<IChatCompletionService>(null, factory3);
                kernel3Builder.Services.AddKeyedScoped<ITextGenerationService>(null, factory3);

                kernelStandard = kernel3Builder.Build();

                if (!string.IsNullOrEmpty(premiumChatGptDeployment))
                {
#pragma warning disable IDE0039 // Use local function
                    Func<IServiceProvider, object?, AzureOpenAIChatCompletionService> factory4 = (serviceProvider, _) => new(premiumChatGptDeployment, premiumChatGptClient, null, serviceProvider.GetService<ILoggerFactory>());
#pragma warning restore IDE0039 // Use local function

                    var kernel4Builder = Kernel.CreateBuilder();
                    kernel4Builder.Services.AddKeyedScoped<IChatCompletionService>(null, factory4);
                    kernel4Builder.Services.AddKeyedScoped<ITextGenerationService>(null, factory4);
                    kernelPremium = kernel4Builder.Build();
                }
            }
            else
            {
                kernelStandard = Kernel.CreateBuilder()
                   .AddAzureOpenAIChatCompletion(standardChatGptDeployment, standardServiceEndpoint, azureCredential)
                   .Build();
                if (!string.IsNullOrEmpty(premiumChatGptDeployment))
                {
                    kernelPremium = Kernel.CreateBuilder()
                   .AddAzureOpenAIChatCompletion(premiumChatGptDeployment, premiumServiceEndpoint, azureCredential)
                   .Build();
                }
            }

            kernelStandard.ImportPluginFromObject(retrieveRelatedDocumentPlugin3, DefaultSettings.DocumentRetrievalPluginName);
            kernelStandard.ImportPluginFromObject(generateSearchQueryPlugin, DefaultSettings.GenerateSearchQueryPluginName);
            kernelStandard.ImportPluginFromObject(chatPlugin, DefaultSettings.ChatPluginName);

            if (kernelPremium != null)
            {
                kernelPremium.ImportPluginFromObject(retrieveRelatedDocumentPlugin4, DefaultSettings.DocumentRetrievalPluginName);
                kernelPremium.ImportPluginFromObject(generateSearchQueryPlugin, DefaultSettings.GenerateSearchQueryPluginName);
                kernelPremium.ImportPluginFromObject(chatPlugin, DefaultSettings.ChatPluginName);
            }
            return new OpenAIClientFacade(standardChatGptDeployment, kernelStandard, premiumChatGptDeployment, kernelPremium);
        });
        services.AddSingleton((sp) =>
        {
            var cosmosDbEndpoint = configuration.CosmosDbEndpoint;
            var client = new CosmosClient(cosmosDbEndpoint, azureCredential);
            return client;
        });

        services.AddSingleton<SearchClientFactory>(sp =>
        {
            return new SearchClientFactory(configuration, azureCredential);
        });

        if (!string.IsNullOrEmpty(configuration.CosmosDbEndpoint))
        {
            services.AddSingleton((sp) =>
            {
                var endpoint = configuration.CosmosDbEndpoint;
                CosmosClientBuilder configurationBuilder = new CosmosClientBuilder(endpoint, azureCredential);
                return configurationBuilder
                        .Build();
            });
        }

        RegisterDomainServices(services, configuration);

        return services;
    }

    private static void RegisterDomainServices(IServiceCollection services, AppConfiguration configuration)
    {
        // Add ChatHistory and document upload services if the connection string is provided
        if (string.IsNullOrEmpty(configuration.CosmosDBConnectionString) && string.IsNullOrEmpty(configuration.CosmosDbEndpoint))
        {
            services.AddScoped<IChatHistoryService, ChatHistoryServiceStub>();
            services.AddScoped<IDocumentService, DocumentServiceSub>();
        }
        else
        {
            services.AddSingleton<IChatHistoryService, ChatHistoryService>();

            var documentUploadStrategy = configuration.DocumentUploadStrategy;
            if (documentUploadStrategy == "AzureNative")
            {
                services.AddSingleton<IDocumentService, DocumentServiceAzureNative>();
                services.AddHttpClient<DocumentServiceAzureNative>();
            }
            else
            {
                services.AddSingleton<IDocumentService, DocumentService>();
                services.AddHttpClient<DocumentService>();
            }
        }

        services.AddSingleton<ChatService>();
        services.AddSingleton<ReadRetrieveReadChatService>();
        services.AddSingleton<ReadRetrieveReadStreamingChatService>();
        services.AddSingleton<EndpointChatService>();
        services.AddSingleton<EndpointChatServiceV2>();
        services.AddSingleton<EndpointTaskService>();
        services.AddSingleton<AzureBlobStorageService>();
        services.AddHttpClient<IngestionService>();
        services.AddHttpClient<EndpointTaskService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });
    }

    private static void SetupOpenAIClientsUsingOnBehalfOfOthersFlowAndSubscriptionKey(IServiceProvider sp, IHttpContextAccessor httpContextAccessor, AppConfiguration config, string? standardServiceEndpoint, out AzureOpenAIClient? openAIClient3, out AzureOpenAIClient? openAIClient4)
    {
        var credential = new OnBehalfOfCredential(
                            tenantId: config.AzureTenantID,
                            clientId: config.AzureServicePrincipalClientID,
                            clientSecret: config.AzureServicePrincipalClientSecret,
                            userAssertion: httpContextAccessor.HttpContext?.Request?.Headers[config.XMsTokenAadAccessToken],
                            new OnBehalfOfCredentialOptions
                            {
                                AuthorityHost = new Uri(config.AzureAuthorityHost)
                            });

        var httpClient = sp.GetService<IHttpClientFactory>().CreateClient();

        //if the configuration specifies a subscription key, add it to the request headers
        if (config.OcpApimSubscriptionKey != null)
        {
            httpClient.DefaultRequestHeaders.Add(config.OcpApimSubscriptionHeaderName, config.OcpApimSubscriptionKey);
        }

        openAIClient3 = new AzureOpenAIClient(new Uri(standardServiceEndpoint), credential, new AzureOpenAIClientOptions
        {
            Audience = config.AzureServicePrincipalOpenAIAudience,
            Transport = new HttpClientPipelineTransport(httpClient)
        });

        openAIClient4 = new AzureOpenAIClient(new Uri(standardServiceEndpoint), credential, new AzureOpenAIClientOptions
        {
            Audience = config.AzureServicePrincipalOpenAIAudience,
            Transport = new HttpClientPipelineTransport(httpClient)
        });
    }

    internal static IServiceCollection AddCrossOriginResourceSharing(this IServiceCollection services)
    {
        services.AddCors(
            options =>
                options.AddDefaultPolicy(
                    policy =>
                        policy.AllowAnyOrigin()
                            .AllowAnyHeader()
                            .AllowAnyMethod()));

        return services;
    }

    internal static IServiceCollection AddCustomHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<CosmosDbReadinessHealthCheck>("CosmosDB Readiness Health Check", failureStatus: HealthStatus.Degraded, tags: ["readiness"]);
        services.AddHealthChecks().AddCheck<AzureStorageReadinessHealthCheck>("Azure Storage Readiness Health Check", failureStatus: HealthStatus.Degraded, tags: ["readiness"]);
        //TODO: this is commented out until a refactor of the profiles is done. The Search Index must exist in order to check its readiness.
        //services.AddHealthChecks().AddCheck<AzureSearchReadinessHealthCheck>("Azure Search Readiness Health Check", failureStatus: HealthStatus.Degraded, tags: ["readiness"]);

        return services;
    }

    internal static WebApplication MapCustomHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
        {
            Predicate = healthCheck => healthCheck.Tags.Contains("readiness"),
            ResponseWriter = WriteResponse
        });

        app.MapHealthChecks("/healthz/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        app.MapHealthChecks("/healthz/startup", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        return app;
    }

    internal static Task WriteResponse(HttpContext context, HealthReport healthReport)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var options = new JsonWriterOptions { Indented = true };

        using var memoryStream = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(memoryStream, options))
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WriteString("status", healthReport.Status.ToString());
            jsonWriter.WriteStartObject("results");

            foreach (var healthReportEntry in healthReport.Entries)
            {
                jsonWriter.WriteStartObject(healthReportEntry.Key);
                jsonWriter.WriteString("status",
                    healthReportEntry.Value.Status.ToString());
                jsonWriter.WriteString("description",
                    healthReportEntry.Value.Description);
                jsonWriter.WriteStartObject("data");

                foreach (var item in healthReportEntry.Value.Data)
                {
                    jsonWriter.WritePropertyName(item.Key);

                    System.Text.Json.JsonSerializer.Serialize(jsonWriter, item.Value,
                        item.Value?.GetType() ?? typeof(object));
                }

                jsonWriter.WriteEndObject();
                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();
        }

        return context.Response.WriteAsync(
            Encoding.UTF8.GetString(memoryStream.ToArray()));
    }

    private static bool UseAPIMAIGatewayOBO(AppConfiguration config)
    {
        return config.AzureServicePrincipalClientID != null
                && config.AzureServicePrincipalClientSecret != null
                && config.AzureTenantID != null
                && config.AzureAuthorityHost != null
                && config.AzureServicePrincipalOpenAIAudience != null;
    }
}
