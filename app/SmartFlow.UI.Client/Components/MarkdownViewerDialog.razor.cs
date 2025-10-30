// Copyright (c) Microsoft. All rights reserved.

using Markdig;

namespace SmartFlow.UI.Client.Components;

public sealed partial class MarkdownViewerDialog
{
    private bool _isLoading = true;
    private string _htmlContent = string.Empty;
    private string _errorMessage = string.Empty;

    [Parameter] public required string FileName { get; set; }
    [Parameter] public required string FileUrl { get; set; }

    [CascadingParameter] public required IMudDialogInstance Dialog { get; set; }
    [Inject] public required HttpClient HttpClient { get; set; }
    [Inject] public required ILogger<MarkdownViewerDialog> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadMarkdownContentAsync();
    }

    private async Task LoadMarkdownContentAsync()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            var response = await HttpClient.GetAsync(FileUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var markdownContent = await response.Content.ReadAsStringAsync();
                
                // Convert Markdown to HTML using Markdig
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .Build();
                
                _htmlContent = Markdown.ToHtml(markdownContent, pipeline);
            }
            else
            {
                _errorMessage = $"Failed to load file: {response.StatusCode}";
                Logger.LogError("Failed to load markdown file {FileName}: {StatusCode}", FileName, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading markdown file: {ex.Message}";
            Logger.LogError(ex, "Error loading markdown file {FileName}", FileName);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private void OnCloseClick() => Dialog.Close(DialogResult.Ok(true));
}
