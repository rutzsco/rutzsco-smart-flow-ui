// Copyright (c) Microsoft. All rights reserved.

using Shared.Models;
using System.Net.Http.Json;

namespace SmartFlow.UI.Client.Services;

/// <summary>
/// Service to load UI configuration from the server
/// </summary>
public class UIConfigurationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UIConfigurationService> _logger;
    private UIConfiguration? _cachedConfig;

    public UIConfigurationService(HttpClient httpClient, ILogger<UIConfigurationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Loads UI configuration from the server
    /// </summary>
    public async Task<UIConfiguration?> LoadConfigurationAsync()
    {
        if (_cachedConfig != null)
        {
            return _cachedConfig;
        }

        try
        {
            _logger.LogInformation("Loading UI configuration from server");
            _cachedConfig = await _httpClient.GetFromJsonAsync<UIConfiguration>("api/config/ui");
            
            if (_cachedConfig != null)
            {
                _logger.LogInformation("UI configuration loaded successfully");
                AppConfiguration.LoadFromServer(_cachedConfig);
            }
            
            return _cachedConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load UI configuration from server");
            return null;
        }
    }

    /// <summary>
    /// Forces a reload of the configuration from the server
    /// </summary>
    public async Task<UIConfiguration?> ReloadConfigurationAsync()
    {
        _cachedConfig = null;
        return await LoadConfigurationAsync();
    }
}
