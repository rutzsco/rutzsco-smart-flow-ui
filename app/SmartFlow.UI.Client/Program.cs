// Copyright (c) Microsoft. All rights reserved.

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register the root component
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Configuration.AddJsonFile($"appsettings.json", optional: true, reloadOnChange: true);

builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection(nameof(AppSettings))
);

// Register typed HttpClient for ApiClient with proper configuration
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Register default HttpClient for components that need it directly
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());
builder.Services.AddLocalStorageServices();
builder.Services.AddSessionStorageServices();
builder.Services.AddMudServices();

// Register UI Configuration Service
builder.Services.AddScoped<UIConfigurationService>();

// Register Global Error Handler
builder.Services.AddSingleton<GlobalErrorHandler>();

AppConfiguration.Load(builder.Configuration);

await JSHost.ImportAsync(
    moduleName: nameof(JavaScriptModule),
    moduleUrl: $"../js/iframe.js?{Guid.NewGuid()}" /* cache bust */);

await builder.Build().RunAsync();
