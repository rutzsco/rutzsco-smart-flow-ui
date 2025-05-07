// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Configuration;

namespace Shared.Models;

public record ProfileInfo
{
    public List<ProfileDefinition> Profiles = [];
    public string ProfileLoadingMessage = string.Empty;
    public string ProfileSource = string.Empty;
    public List<ProfileKey> Keys = [];

    public ProfileInfo()
    {
    }
    public ProfileInfo(string profile1Name, string profileSource, string profileLoadingMessage)
    {
        Profiles = [new(profile1Name)];
        ProfileLoadingMessage = profileLoadingMessage;
        ProfileSource = profileSource;
    }
    public ProfileInfo(List<ProfileDefinition> profiles, string profileSource, string profileLoadingMessage, IConfiguration configuration)
    {
        Profiles = profiles;
        ProfileSource = profileSource;
        ProfileLoadingMessage = profileLoadingMessage;
        EvaluateKeys(Profiles, configuration);
    }
    public void EvaluateKeys(List<ProfileDefinition> profiles, IConfiguration configuration)
    {
        Keys = [];
        foreach (var profile in profiles)
        {
            var epSettingName = profile.AssistantEndpointSettings?.APIEndpointSetting ?? string.Empty;
            var epSettingValue = !string.IsNullOrEmpty(epSettingName) ? configuration[epSettingName] : string.Empty;
            var epSettingIsValid = !string.IsNullOrEmpty(epSettingValue);

            var epKeyName = profile.AssistantEndpointSettings?.APIEndpointKeySetting ?? string.Empty;
            var epKeyValue = !string.IsNullOrEmpty(epKeyName) ? configuration[epKeyName] : string.Empty;
            var epKeyIsValid = !string.IsNullOrEmpty(epKeyValue);

            var keys = new ProfileKey
            {
                ProfileName = profile.Name,
                ProfileId = profile.Id,
                APIEndpointSettingName = epSettingName,
                APIEndpointSettingValue = epSettingValue ?? string.Empty,
                APIEndpointSettingIsValid = epSettingIsValid,
                APIEndpointKeySettingName = epKeyName,
                APIEndpointKeySettingIsValid = epKeyIsValid
            };
            Keys.Add(keys);
        }
    }
}
public record ProfileKey
{
    public string ProfileName { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string APIEndpointSettingName { get; set; } = string.Empty;
    public string APIEndpointSettingValue { get; set; } = string.Empty;
    public bool APIEndpointSettingIsValid { get; set; } = false;
    public string APIEndpointKeySettingName { get; set; } = string.Empty;
    public bool APIEndpointKeySettingIsValid { get; set; } = false;
    public bool AllowFileUpload { get; set; } = false;
}
