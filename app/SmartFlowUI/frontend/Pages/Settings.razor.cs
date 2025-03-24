// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace ClientApp.Pages;

public sealed partial class Settings : IDisposable
{
    private MudForm _form = null!;
    private MudForm _encodeForm = null!;

    private bool _isLoadingProfiles = false;
    private ProfileInfo _profileInfo = new ProfileInfo();
    private string _profileRawData = string.Empty;
    private string _b64DecodedText = string.Empty;
    private string _b64EncodedText = string.Empty;
    private Models.BuildInfo _buildInfo = BuildInfo.Instance;

    // Store a cancelation token that will be used to cancel if the user disposes of this component.
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    [Inject] public required ApiClient Client { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required ILogger<Docs> Logger { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }
    [Inject] public required HttpClient httpClient { get; set; }

    protected override void OnInitialized()
    {
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            await GetProfileInfoAsync();
            StateHasChanged();
        }
    }

    private async Task GetProfileInfoAsync()
    {
        _isLoadingProfiles = true;
        var profileData = string.Empty;
        try
        {
            (_profileInfo, _profileRawData) = await Client.GetProfilesInfoAsync();
            if (_profileInfo == null) { _profileInfo = new ProfileInfo(); }
        }
        catch (Exception ex)
        {
            showWarning($"Failed to read profileInfo! {ex.Message}");
            showWarning(profileData);
        }
        _isLoadingProfiles = false;
        StateHasChanged();
    }
    private async Task ReloadProfileInfoAsync()
    {
        _isLoadingProfiles = true;
        showInfo("Calling reloadProfileInfo...");
        var profileData = string.Empty;
        try
        {
            (_profileInfo, _profileRawData) = await Client.GetProfilesReloadAsync();
            if (_profileInfo == null) { _profileInfo = new ProfileInfo(); }
        }
        catch (Exception ex)
        {
            showWarning($"Failed to reload profileInfo! {ex.Message}");
            showWarning(profileData);
        }
        _isLoadingProfiles = false;
        StateHasChanged();
    }
    private async Task RefreshAsync()
    {
        await GetProfileInfoAsync();
    }

    private void Base64EncodeText()
    {
        if (!string.IsNullOrEmpty(_b64DecodedText))
        {
            var bytes = Encoding.UTF8.GetBytes(_b64DecodedText);
            _b64EncodedText = Convert.ToBase64String(bytes);
        }
    }
    private void Base64DecodeText()
    {
        if (!string.IsNullOrEmpty(_b64EncodedText))
        {
            var bytes = Convert.FromBase64String(_b64EncodedText);
            _b64DecodedText = Encoding.UTF8.GetString(bytes);
        }
    }

    private void showInfo(string message)
    {
        showMessage(message, Severity.Info);
    }
    private void showWarning(string message)
    {
        showMessage(message, Severity.Warning);
    }
    private void showMessage(string message, Severity severity)
    {
        Snackbar.Add(
            message,
            severity,
            static options =>
            {
                options.ShowCloseIcon = true;
                options.VisibleStateDuration = 10_000;
            });
    }

    public void Dispose() => _cancellationTokenSource.Cancel();
}
