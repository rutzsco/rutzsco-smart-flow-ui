// Copyright (c) Microsoft. All rights reserved.

using SmartFlow.UI.Client.Services;

namespace SmartFlow.UI.Client.Pages;

public sealed partial class Ingestion
{
    [Inject]
    public required ApiClient Client { get; set; }
    private string _sourceContinerName = string.Empty;
    private string _indexName = string.Empty;


    protected override void OnInitialized()
    {
    }

    private async Task SubmitAsync()
    {

    }
}
