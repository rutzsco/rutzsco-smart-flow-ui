// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Services.Search.IndexDefinitions;

public class KwiecienCustomIndexDefinitionV2 : IKnowledgeSource
{
    public required string content { get; set; }

    public required string sourcefile { get; set; }

    public required string sourcepage { get; set; }

    public required int pagenumber { get; set; }


    public KnowledgeSource GetSource(bool useSourcepage = false)
    {
        return new KnowledgeSource(GetFilepath(useSourcepage), content);
    }

    public string GetFilepath(bool useSourcepage = false)
    {
        if (useSourcepage)
            return sourcepage;

        return $"{sourcefile}#page={pagenumber}";
    }


    public static string EmbeddingsFieldName = "embeddings";
    public static List<string> SelectFieldNames = new List<string> { "content", "sourcefile", "sourcepage", "pagenumber" };
    public static string Name = "KwiecienV2";
}
