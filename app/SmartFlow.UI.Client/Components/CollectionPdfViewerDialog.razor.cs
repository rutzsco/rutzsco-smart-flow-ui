// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.UI.Client.Components;

public sealed partial class CollectionPdfViewerDialog
{
    private bool _isLoading = true;
    private string _pdfViewerVisibilityStyle => _isLoading ? "display:none;" : "display:default;";

    [Parameter] public required string FileName { get; set; }
    [Parameter] public required string FileUrl { get; set; }

    [CascadingParameter] public required IMudDialogInstance Dialog { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await JavaScriptModule.RegisterIFrameLoadedAsync(
            "#pdf-viewer-collection",
            () =>
            {
                _isLoading = false;
                StateHasChanged();
            });
    }

    private void OnCloseClick() => Dialog.Close(DialogResult.Ok(true));
}
