// Copyright (c) Microsoft. All rights reserved.

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register the root component
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

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

await JSHost.ImportAsync(
    moduleName: nameof(JavaScriptModule),
    moduleUrl: $"../js/iframe.js?{Guid.NewGuid()}" /* cache bust */);

await builder.Build().RunAsync();
