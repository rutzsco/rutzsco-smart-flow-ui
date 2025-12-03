// Copyright (c) Microsoft. All rights reserved.

using Shared.Models;
using Microsoft.AspNetCore.DataProtection;
using MinimalApi;
using Azure;
using Azure.Identity;
using Azure.AI.Agents.Persistent;
using Microsoft.SemanticKernel.Agents.AzureAI;
using MinimalApi.M365;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http.Features;

#pragma warning disable SKEXP0110

Console.WriteLine("Starting SmartFlowUI backend... {0}", BuildInfo.Instance);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

// Configure Kestrel to accept larger request bodies (500MB)
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000; // 500 MB
});

// Configure IIS to accept larger request bodies (500MB)
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 524_288_000; // 500 MB
});

// Configure form options for multipart form data (500MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
    options.ValueLengthLimit = 524_288_000; // 500 MB
    options.MultipartHeadersLengthLimit = 524_288_000; // 500 MB
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOutputCache();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddCrossOriginResourceSharing();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// bind configuration
builder.Services.AddOptions<AppConfiguration>()
.Bind(builder.Configuration)
.PostConfigure(options =>
{
    // set default values for options
    options.ApplicationInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    options.AzureServicePrincipalClientID = builder.Configuration["AZURE_SP_CLIENT_ID"];
    options.AzureServicePrincipalClientSecret = builder.Configuration["AZURE_SP_CLIENT_SECRET"];
    options.AzureTenantID = builder.Configuration["AZURE_TENANT_ID"];
    options.AzureAuthorityHost = builder.Configuration["AZURE_AUTHORITY_HOST"];
    options.AzureServicePrincipalOpenAIAudience = builder.Configuration["AZURE_SP_OPENAI_AUDIENCE"];
})
.ValidateDataAnnotations()
.ValidateOnStart();

var appConfiguration = new AppConfiguration();
builder.Configuration.Bind(appConfiguration);

// Add Azure services to the container - unified method handles both key-based and MI credentials
builder.Services.AddAzureServices(appConfiguration);

builder.Services.AddAntiforgery(options => { options.HeaderName = "X-CSRF-TOKEN-HEADER"; options.FormFieldName = "X-CSRF-TOKEN-FORM"; });

builder.Services.AddSingleton<AppConfiguration>();
builder.Services.AddSingleton<ProfileService>();

// Add M365 Agent services
builder.Services.AddM365AgentServices();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    // set application telemetry
    if (!string.IsNullOrEmpty(appConfiguration.ApplicationInsightsConnectionString))
    {
        builder.Services.AddApplicationInsightsTelemetry((option) =>
        {
            option.ConnectionString = appConfiguration.ApplicationInsightsConnectionString;
        });
    }

    if (appConfiguration.EnableDataProtectionBlobKeyStorage)
    {
        var containerName = appConfiguration.DataProtectionKeyContainer;
        var storageAccunt = appConfiguration.AzureStorageAccountEndpoint;
        var fileName = "keys.xml";

        builder.Services.AddDataProtection().PersistKeysToAzureBlobStorage(storageAccunt, containerName, fileName)
            .SetApplicationName("SmartFlowUI")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
    }
}

builder.Services.AddCustomHealthChecks();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseOutputCache();
app.UseRouting();
app.UseStaticFiles();
app.UseCors();
app.UseBlazorFrameworkFiles();
app.UseAntiforgery();
app.MapRazorPages();
app.MapControllers();
app.Use(next => context =>
{
    var antiforgery = app.Services.GetRequiredService<IAntiforgery>();
    var tokens = antiforgery.GetAndStoreTokens(context);
    context.Response.Cookies.Append("XSRF-TOKEN", tokens?.RequestToken ?? string.Empty, new CookieOptions() { HttpOnly = false });
    return next(context);
});
app.MapFallbackToFile("index.html");

// Map agent management API if either Azure AI Foundry or Custom Agent Endpoint is configured
if (!string.IsNullOrEmpty(appConfiguration.AzureAIFoundryProjectEndpoint) || 
    !string.IsNullOrEmpty(builder.Configuration["CustomAgentEndpoint"]))
{
    app.MapAgentManagementApi();
}

app.MapChatApi();
app.MapApi();
app.MapCollectionApi();
app.MapProjectApi();
app.MapVoiceLiveApi();

// Map M365 Agent endpoints
app.MapM365AgentEndpoints();

app.MapCustomHealthChecks();

app.Run();












