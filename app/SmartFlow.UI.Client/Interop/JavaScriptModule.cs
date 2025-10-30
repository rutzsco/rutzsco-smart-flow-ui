// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.UI.Client.Interop;

internal sealed partial class JavaScriptModule
{
    [JSImport("listenForIFrameLoaded", nameof(JavaScriptModule))]
    public static partial Task RegisterIFrameLoadedAsync(
        string selector,
        [JSMarshalAs<JSType.Function>] Action onLoaded);
}
