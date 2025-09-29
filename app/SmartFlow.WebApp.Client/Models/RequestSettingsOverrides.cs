// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.WebApp.Client.Models;

public record RequestSettingsOverrides
{
    public RequestOverrides Overrides { get; set; } = new();
}
