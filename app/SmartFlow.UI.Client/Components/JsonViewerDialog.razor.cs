// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;

namespace SmartFlow.UI.Client.Components;

public sealed partial class JsonViewerDialog
{
    private bool _isLoading = true;
    private string _formattedJson = string.Empty;
    private string _rawJson = string.Empty;
    private string _errorMessage = string.Empty;

    [Parameter] public required string FileName { get; set; }
    [Parameter] public required string FileUrl { get; set; }

    [CascadingParameter] public required IMudDialogInstance Dialog { get; set; }
    [Inject] public required HttpClient HttpClient { get; set; }
    [Inject] public required ILogger<JsonViewerDialog> Logger { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadJsonContentAsync();
    }

    private async Task LoadJsonContentAsync()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            var response = await HttpClient.GetAsync(FileUrl);
            
            if (response.IsSuccessStatusCode)
            {
                _rawJson = await response.Content.ReadAsStringAsync();
                
                // Try to parse and format the JSON
                try
                {
                    var jsonDocument = JsonDocument.Parse(_rawJson);
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    _formattedJson = JsonSerializer.Serialize(jsonDocument, options);
                }
                catch (JsonException jsonEx)
                {
                    _errorMessage = $"Invalid JSON format: {jsonEx.Message}";
                    Logger.LogError(jsonEx, "Failed to parse JSON file {FileName}", FileName);
                }
            }
            else
            {
                _errorMessage = $"Failed to load file: {response.StatusCode}";
                Logger.LogError("Failed to load JSON file {FileName}: {StatusCode}", FileName, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading JSON file: {ex.Message}";
            Logger.LogError(ex, "Error loading JSON file {FileName}", FileName);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task CopyToClipboardAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", _formattedJson);
            
            Snackbar.Add("JSON copied to clipboard", Severity.Success, options =>
            {
                options.ShowCloseIcon = true;
                options.VisibleStateDuration = 3000;
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error copying JSON to clipboard");
            Snackbar.Add("Failed to copy to clipboard", Severity.Error, options =>
            {
                options.ShowCloseIcon = true;
                options.VisibleStateDuration = 3000;
            });
        }
    }

    private void OnCloseClick() => Dialog.Close(DialogResult.Ok(true));
}
