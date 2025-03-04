// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Services.Profile;

public static class ProfileService
{
    private static List<ProfileDefinition> _profileData = [];
    private static string _loadingMessage = string.Empty;
    private static string _profileSource = string.Empty;
    private static BlobServiceClient _blobClient = null;
    private static IConfiguration _config = null;

    public static ProfileInfo GetProfileData()
    {
        var profileInfo = new ProfileInfo(_profileData,
            string.IsNullOrWhiteSpace(_profileSource) ? "No source location found!" : _profileSource,
            string.IsNullOrWhiteSpace(_loadingMessage) ? "No loading message found!" : _loadingMessage,
            _config);
        return profileInfo;
    }

    public static ProfileInfo Reload()
    {
        Load(_config, _blobClient);
        return GetProfileData();
    }

    public static List<ProfileDefinition> Load(IConfiguration configuration, BlobServiceClient blobServiceClient)
    {
        _blobClient = blobServiceClient;
        _config = configuration;

        var profileConfigurationBlobStorageContainer = configuration["ProfileConfigurationBlobStorageContainer"];
        if (!string.IsNullOrEmpty(profileConfigurationBlobStorageContainer))
        {
            _loadingMessage += $"Found Profile storage container name, looking for profiles.json there... ";
            var container = blobServiceClient.GetBlobContainerClient(profileConfigurationBlobStorageContainer);
            var blobClient = container.GetBlobClient("profiles.json");
            var downloadResult = blobClient.DownloadContent();
            var profileStorageData = System.Text.Json.JsonSerializer.Deserialize<List<ProfileDefinition>>(Encoding.UTF8.GetString(downloadResult.Value.Content));
            if (profileStorageData != null)
            {
                _loadingMessage = $"{profileStorageData.Count} profiles were loaded from storage file at {DateTime.Now:MMM-dd HH:mm.ss}!";
                _profileSource = "Storage";
                Console.WriteLine(_loadingMessage);
                _profileData = profileStorageData;
                return profileStorageData;
            }
        }

        var profileConfig = configuration["ProfileConfiguration"];
        if (!string.IsNullOrEmpty(profileConfig))
        {
            _loadingMessage += $"Found Profile Configuration Key, decoding the value... ";
            var bytes = Convert.FromBase64String(profileConfig);
            var profileConfigData = System.Text.Json.JsonSerializer.Deserialize<List<ProfileDefinition>>(Encoding.UTF8.GetString(bytes));
            if (profileConfigData != null)
            {
                _loadingMessage = $"{profileConfigData.Count} profiles were loaded from Config Key at {DateTime.Now:MMM-dd HH:mm.ss}!";
                _profileSource = "Config";
                Console.WriteLine(_loadingMessage);
                _profileData = profileConfigData;
                return profileConfigData;
            }
        }

        _loadingMessage += $"Loading Profile from project embedded file... ";
        var fileName = configuration["ProfileFileName"];
        fileName ??= "profiles";
        var profileFileData = LoadEmbeddedProflies(fileName);
        _loadingMessage = $"{profileFileData.Count} profiles were loaded from embedded file at {DateTime.Now:MMM-dd HH:mm.ss}!";
        _profileSource = "Embedded";
        Console.WriteLine(_loadingMessage);
        _profileData = profileFileData;
        return profileFileData;
    }

	private static List<ProfileDefinition> LoadEmbeddedProflies(string name)
	{
        var resourceName = $"MinimalApi.Services.Profile.{name}.json";
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(resourceName) ?? throw new ArgumentException($"The resource {resourceName} was not found.");
        using StreamReader reader = new(stream);
        var jsonText = reader.ReadToEnd();
        var profiles = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ProfileDefinition>>(jsonText);
        return profiles ?? [];
	}
}
