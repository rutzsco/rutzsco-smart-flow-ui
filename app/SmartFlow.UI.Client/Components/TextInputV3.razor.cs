// Copyright (c) Microsoft. All rights reserved.

using SmartFlow.UI.Client.Models;

namespace SmartFlow.UI.Client.Components;

public sealed partial class TextInputV3
{
    private List<FileSummary> _files = new List<FileSummary>();


    [Parameter] public EventCallback<FileSummary> OnFileUpload { get; set; }
    [Parameter] public EventCallback<string> OnEnterKeyPressed { get; set; }
    [Parameter] public EventCallback OnResetPressed { get; set; }

    [Parameter] public EventCallback<bool> OnModelSelection { get; set; }

    [Parameter] public required string UserQuestion { get; set; }

    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool SupportsFileUpload { get; set; }

    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string Placeholder { get; set; } = "";

    [Parameter] public string ImageUrl { get; set; } = "";

    private async Task OnKeyUpAsync(KeyboardEventArgs args)
    {
        if (args is { Key: "Enter", ShiftKey: false } && OnEnterKeyPressed.HasDelegate)
        {
            var question = UserQuestion;
            UserQuestion = string.Empty;
            question.TrimEnd('\n');
            await OnEnterKeyPressed.InvokeAsync(question);
        }
    }
    private async Task OnAskClickedAsync()
    {
        await OnEnterKeyPressed.InvokeAsync(UserQuestion);
        UserQuestion = string.Empty;
    }
    private async Task OnClearChatAsync()
    {
        UserQuestion = "";
        _files.Clear();
        await OnResetPressed.InvokeAsync();
    }
    private async Task OnModelSelectionAsync(bool toggle)
    {
        await OnModelSelection.InvokeAsync(toggle);
    }

    private async Task UploadFilesAsync(IBrowserFile file)
    {
        var buffer = new byte[file.Size];
        await file.OpenReadStream(104857600).ReadAsync(buffer);
        var imageContent = Convert.ToBase64String(buffer);

        var fileSummary = new FileSummary($"data:{file.ContentType};base64,{imageContent}", file.Name, file.ContentType);
        _files.Add(fileSummary);

        await OnFileUpload.InvokeAsync(fileSummary);
    }
}
