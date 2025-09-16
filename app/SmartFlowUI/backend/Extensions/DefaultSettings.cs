using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace MinimalApi.Extensions;

public static class DefaultSettings
{
    public static PromptExecutionSettings AIChatWithToolsRequestSettings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(), Temperature = 0.0, MaxTokens = 2024, TopP = 1 };
    public static PromptExecutionSettings AIChatRequestSettings = new OpenAIPromptExecutionSettings { Temperature = 0.0, MaxTokens = 2024, TopP = 1 };
    public static string CosmosDbDatabaseName = "ChatHistory";
    public static string CosmosDbCollectionName = "ChatTurn";
    public static string CosmosDBUserDocumentsCollectionName = "UserDocuments";
}
