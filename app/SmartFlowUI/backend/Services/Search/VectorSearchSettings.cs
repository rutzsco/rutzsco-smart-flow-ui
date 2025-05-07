// Copyright (c) Microsoft. All rights reserved.
namespace MinimalApi.Services.Search;

public record VectorSearchSettings(string IndexName, int DocumentCount, string IndexSchemaDefinition, string EmbeddingModelName, int MaxSourceTokens, int KNearestNeighborsCount, bool Exhaustive, bool UseSemanticRanker, string SemanticConfigurationName, string SourceContainer, bool CitationUseSourcePage);

//public static class Extensions
//{
//    public static VectorSearchSettings ToVectorSearchSettings(this RAGSettingsSummary ragSettings)
//    {
//        return new VectorSearchSettings(ragSettings.KNearestNeighborsCount, ragSettings.Exhaustive, ragSettings.UseSemanticRanker, ragSettings.SemanticConfigurationName, ragSettings.CitationUseSourcePage);
//    }
//}
