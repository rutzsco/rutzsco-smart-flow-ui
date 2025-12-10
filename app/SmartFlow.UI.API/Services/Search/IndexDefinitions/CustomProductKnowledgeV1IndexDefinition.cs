// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Services.Search.IndexDefinitions;

public class CustomProductKnowledgeV1IndexDefinition : IKnowledgeSource
{
    public required string chunk_id { get; set; }

    public required string parent_id { get; set; }

    public required int chunk_index { get; set; }

    public required string content { get; set; }

    public required string title { get; set; }

    public required string blob_path { get; set; }

    public required string file_name { get; set; }

    public string? content_type { get; set; }

    public string? equipment_category { get; set; }

    public string? equipment_subcategory { get; set; }

    public string? equipment_part { get; set; }

    public string? equipment_part_subcategory { get; set; }

    public string? product { get; set; }

    public string? manufacturer { get; set; }

    public string? document_type { get; set; }

    public bool is_required_for_cde { get; set; }
    
    public KnowledgeSource GetSource(bool useSourcepage = false)
    {
        return new KnowledgeSource(GetFilepath(useSourcepage), content);
    }

    public string GetFilepath(bool useSourcepage = false)
    {
        // Use metadata as the source identifier, fallback to id if metadata is empty
        return !string.IsNullOrEmpty(blob_path) ? blob_path : chunk_id;
    }

    public static string EmbeddingsFieldName = "content_vector";
    public static List<string> SelectFieldNames = new List<string> { "chunk_id", "parent_id", "chunk_index", "title", "content", "blob_path", "file_name", "content_type", "equipment_category", "equipment_subcategory", "equipment_part", "equipment_part_subcategory", "product", "manufacturer", "document_type", "is_required_for_cde" };
    public static string Name = "CustomProductKnowledgeV1";
}
