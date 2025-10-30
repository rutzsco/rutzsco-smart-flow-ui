// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.UI.Client.Models;

public record class SharedCultures
{
    [JsonPropertyName("translation")]
    public required IDictionary<string, AzureCulture> AvailableCultures { get; set; }
}
