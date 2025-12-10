// Copyright (c) Microsoft. All rights reserved.

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Configuration.AddJsonFile($"appsettings.json", optional: true, reloadOnChange: true);

builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection(nameof(AppSettings))
);
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress), Timeout = TimeSpan.FromMinutes(5) });
builder.Services.AddLocalStorageServices();
builder.Services.AddSessionStorageServices();
builder.Services.AddMudServices();

// Register UI Configuration Service
builder.Services.AddScoped<UIConfigurationService>();

AppConfiguration.Load(builder.Configuration);

try
{
    await JSHost.ImportAsync(
        moduleName: nameof(JavaScriptModule),
        moduleUrl: $"/js/iframe.js?{Guid.NewGuid()}" /* cache bust */);
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to import JavaScript module: {ex.Message}");
}

await builder.Build().RunAsync();
