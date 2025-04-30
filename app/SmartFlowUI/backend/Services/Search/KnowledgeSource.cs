// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Services.Search;


public record KnowledgeSource(string FilePath, string Content);


public interface IKnowledgeSource
{
    KnowledgeSource GetSource(bool useSourcepage = false);
}

public record KnowledgeSourceSummary(List<KnowledgeSource> Sources);
