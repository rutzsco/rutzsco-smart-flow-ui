// Copyright (c) Microsoft. All rights reserved.

using System.Text.RegularExpressions;

namespace MinimalApi.Services.Search.IndexDefinitions;
public class AISearchIndexerIndexDefinintion : IKnowledgeSource
{
    public required string title { get; set; }

    public required string chunk { get; set; }

    public required string chunk_id { get; set; }

    public KnowledgeSource GetSource(bool useSourcepage = false)
    {
        return new KnowledgeSource(GetFilepath(useSourcepage), chunk);
    }

    public string GetFilepath(bool useSourcepage = false)
    {
        return title;
    }

    public static string EmbeddingsFieldName = "text_vector";
    public static List<string> SelectFieldNames = new List<string> { "title", "chunk_id", "chunk" };
    public static string Name = "AISearchV1";
}
