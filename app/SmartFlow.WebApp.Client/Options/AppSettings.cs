// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.WebApp.Client.Options;

public class AppSettings
{
    [ConfigurationKeyName("BACKEND_URI")]
    public string BackendUri { get; set; } = "https://localhost:7181"!;
}
