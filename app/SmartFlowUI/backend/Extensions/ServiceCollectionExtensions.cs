// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MinimalApi.Services.HealthChecks;
using System.ClientModel.Primitives;
using Microsoft.Extensions.Azure;
using MinimalApi.Agents;

namespace MinimalApi.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddAzureServices(this IServiceCollection services, AppConfiguration configuration)
    {
        var sp = services.BuildServiceProvider();
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();

        DefaultAzureCredential azureCredential;
        services.AddAzureClients(builder =>
        {
            if (!string.IsNullOrEmpty(configuration.VisualStudioTenantId))
            {
                azureCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    VisualStudioTenantId = configuration.VisualStudioTenantId
                });
                builder.UseCredential(azureCredential);
            }
            else
            {
                if (!string.IsNullOrEmpty(configuration.UserAssignedManagedIdentityClientId))
                {
                    azureCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        ManagedIdentityClientId = configuration.UserAssignedManagedIdentityClientId
                    });
                    builder.UseCredential(azureCredential);
                }
                else
                {
                    azureCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions());
                    builder.UseCredential(azureCredential);
                }
            }

            builder.AddBlobServiceClient(configuration.AzureStorageAccountEndpoint);
        });

        // Register TokenCredential for DI
        services.AddSingleton<TokenCredential>(sp => 
        {
            if (!string.IsNullOrEmpty(configuration.VisualStudioTenantId))
            {
                return new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    VisualStudioTenantId = configuration.VisualStudioTenantId
                });
            }
            else
            {
                if (!string.IsNullOrEmpty(configuration.UserAssignedManagedIdentityClientId))
                {
                    return new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        ManagedIdentityClientId = configuration.UserAssignedManagedIdentityClientId
                    });
                }
                else
                {
                    return new DefaultAzureCredential(new DefaultAzureCredentialOptions());
                }
            }
        });

        services.AddSingleton<BlobContainerClient>(sp =>
        {
            var azureStorageContainer = configuration.AzureStorageContainer;
            return sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(azureStorageContainer);
        });

        services.AddSingleton<OpenAIClientFacade>(sp =>
        {
            var facade = new OpenAIClientFacade(configuration, new Azure.AzureKeyCredential(configuration.AOAIStandardServiceKey), null, sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<SearchClientFactory>());
            return facade;
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

        // Register TokenCredential for DI
        services.AddSingleton<TokenCredential>(azureCredential);

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

        services.AddSingleton<OpenAIClientFacade>(sp =>
        {
            var facade = new OpenAIClientFacade(configuration, null, azureCredential, sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<SearchClientFactory>());
            return facade;
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


        services.AddSingleton<AzureAIAgentManagementService>();
        services.AddSingleton<ImageGenerationChatAgent>();
        services.AddSingleton<TextToImageService>();
        services.AddSingleton<ChatService>();
        services.AddSingleton<RAGChatService>();
        services.AddSingleton<AzureAIAgentChatService>();
        services.AddSingleton<EndpointChatService>();
        services.AddSingleton<EndpointChatServiceV2>();
        services.AddSingleton<EndpointTaskService>();
        services.AddSingleton<AzureBlobStorageService>();
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
