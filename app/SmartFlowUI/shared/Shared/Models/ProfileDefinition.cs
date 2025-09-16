// Copyright (c) Microsoft. All rights reserved.

namespace Shared.Models;

public class ProfileDefinition
{
    public ProfileDefinition()
    {
        Name = "Undefined";
    }
    public ProfileDefinition(string name)
    {
        Name = name;
    }
    public ProfileDefinition(
        string name,
        string id,
        string approach,
        string securityModel,
        bool allowFileUpload,
        string? azureAIAgentID,
        List<string> securityModelGroupMembership,
        List<string> sampleQuestions,
        RAGSettingsSummary? ragSettingsSummary,
        AssistantEndpointSettingsSummary? assistantEndpointSettingsSummary)
    {
        Name = name;
        Id = id;
        Approach = approach;
        SecurityModel = securityModel;
        AllowFileUpload = allowFileUpload;
        AzureAIAgentID = azureAIAgentID;
        SampleQuestions = sampleQuestions;
        RAGSettings = ragSettingsSummary;
        AssistantEndpointSettings = assistantEndpointSettingsSummary;
        SecurityModelGroupMembership = securityModelGroupMembership ?? ([]);
    }

    public string Name { get; set; }
    public string Id { get; set; }
    public string Approach { get; set; }
    public string SecurityModel { get; set; }
    public bool AllowFileUpload { get; set; }
    public List<string> SecurityModelGroupMembership { get; set; }
    public RAGSettingsSummary? RAGSettings { get; set; }
    public AssistantEndpointSettingsSummary? AssistantEndpointSettings { get; set; }
    public string ChatSystemMessageFile { get; set; }
    public string ChatSystemMessage { get; set; }
    public List<string> SampleQuestions { get; set; }
    public List<UserPromptTemplate> UserPromptTemplates { get; set; }
    public string? AzureAIAgentID { get; set; }
}

public class RAGSettingsSummary
{
    public required string DocumentRetrievalSchema { get; set; }
    public required string DocumentRetrievalEmbeddingsDeployment { get; set; }
    public required string DocumentRetrievalIndexName { get; set; }
    public string? DocumentIndexerName { get; set; }
    public required int DocumentRetrievalDocumentCount { get; set; }
    public required int DocumentRetrievalMaxSourceTokens { get; set; } = 12000;
    public required string ChatSystemMessage { get; set; }
    public required string ChatUserMessage { get; set; }
    public required string ChatSystemMessageFile { get; set; }
    public required string StorageContianer { get; set; }
    public required bool CitationUseSourcePage { get; set; }
    public required bool UseSemanticRanker { get; set; }
    public string? SemanticConfigurationName { get; set; }
    public required int KNearestNeighborsCount { get; set; } = 3;
    public required bool Exhaustive { get; set; } = false;
    
    public required IEnumerable<ProfileUserSelectionOption> ProfileUserSelectionOptions { get; set; }
}

public class ProfileUserSelectionOption
{
    public required string DisplayName { get; set; }
    public required string IndexFieldName { get; set; }
}

public class DocumentCollectionRAGSettings
{
    public required string GenerateSearchQueryPluginName { get; set; }
    public required string GenerateSearchQueryPluginQueryFunctionName { get; set; }
    public required string DocumentRetrievalPluginName { get; set; }
    public required string DocumentRetrievalPluginQueryFunctionName { get; set; }
    public required string DocumentRetrievalIndexName { get; set; }
    public int DocumentRetrievalDocumentCount { get; set; }
    public required string ChatSystemMessageFile { get; set; }
    public required string StorageContianer { get; set; }
}

public class AssistantEndpointSettingsSummary
{
    public required string APIEndpointSetting { get; set; }
    public required string APIEndpointKeySetting { get; set; }
    public required bool AllowFileUpload { get; set; }
}
