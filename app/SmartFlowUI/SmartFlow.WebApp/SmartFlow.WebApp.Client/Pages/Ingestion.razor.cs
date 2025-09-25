// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.WebApp.Client.Pages;

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
