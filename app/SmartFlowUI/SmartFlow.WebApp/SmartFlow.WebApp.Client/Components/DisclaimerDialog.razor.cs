// Copyright (c) Microsoft. All rights reserved.

using static MudBlazor.CategoryTypes;

namespace SmartFlow.WebApp.Client.Components;

public sealed partial class DisclaimerDialog
{

    [CascadingParameter] public required IMudDialogInstance Dialog { get; set; }

    private string _disclaimerMessage;

    private void OnCloseClick() => Dialog.Close(DialogResult.Ok(true));

    protected override void OnParametersSet()
    {
        _disclaimerMessage = AppConfiguration.DisclaimerMessage;
        base.OnParametersSet();
    }
}
