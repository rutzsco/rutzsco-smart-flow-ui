// Copyright (c) Microsoft. All rights reserved.

using Newtonsoft.Json;

namespace ClientApp.Models;

/// <summary>
/// Build Info
/// </summary>
public class BuildInfo
{
    /// <summary>
    /// Build Date
    /// </summary>
    [JsonProperty("buildDate")]
    public string BuildDate { get; set; }

    /// <summary>
    /// Build Number
    /// </summary>
    [JsonProperty("buildNumber")]
    public string BuildNumber { get; set; }

    /// <summary>
    /// Build Id
    /// </summary>
    [JsonProperty("buildId")]
    public string BuildId { get; set; }

    /// <summary>
    /// Branch Name
    /// </summary>
    [JsonProperty("branchName")]
    public string BranchName { get; set; }

    /// <summary>
    /// Commit Hash
    /// </summary>
    [JsonProperty("commitHash")]
    public string CommitHash { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public BuildInfo()
    {
        BuildDate = string.Empty;
        BuildNumber = string.Empty;
        BuildId = string.Empty;
        BranchName = string.Empty;
        CommitHash = string.Empty;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    public BuildInfo(string buildDate, string buildNumber, string buildId, string branchName, string commitHash)
    {
        BuildDate = buildDate;
        BuildNumber = buildNumber;
        BuildId = buildId;
        BranchName = branchName;
        CommitHash = commitHash;
    }
}
